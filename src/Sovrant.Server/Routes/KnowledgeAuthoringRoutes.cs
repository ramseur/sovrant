using System.Text.Json;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Knowledge;
using Sovrant.Server.Auth;
using Sovrant.Tools.Templates;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers authoring endpoints under <c>/v1/knowledge/{kind}/{slug}</c> for the
/// Knowledge UI's markdown editor. <c>kind</c> ∈ <c>skills | agents | documents | tools | guidelines</c>.
/// Phase 112: skills and agents are DB-only (no filesystem). Documents and tools are still
/// dual-registered pending Phase C.
/// </summary>
internal static class KnowledgeAuthoringRoutes
{
    public static void Map(WebApplication app)
    {
        // Read source markdown for editor pre-load.
        app.MapGet("/v1/knowledge/{kind}/{slug}/source", async (
            string kind,
            string slug,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools,
            IKnowledgeStore knowledgeStore) =>
        {
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            return kind.ToLowerInvariant() switch
            {
                "guidelines" => await GetGuidelineSourceAsync(slug, knowledgeStore).ConfigureAwait(false),
                "skills"     => await GetDbSourceAsync("skills", slug, knowledgeStore).ConfigureAwait(false),
                "agents"     => await GetDbSourceAsync("agents", slug, knowledgeStore).ConfigureAwait(false),
                "documents"  => GetDocSource(slug, docs),
                "tools"      => GetToolSource(slug, tools),
                _            => Results.NotFound(new { error = $"Unknown kind '{kind}'." }),
            };
        });

        // Save markdown to user (global) tier.
        app.MapPost("/v1/knowledge/{kind}/{slug}", async (
            string kind,
            string slug,
            HttpRequest request,
            HttpContext ctx,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools,
            IKnowledgeStore knowledgeStore) =>
        {
            if (!HttpContextAuthExtensions.IsAdmin(ctx))
                return Results.Json(new { error = "Admin role required to modify knowledge." }, statusCode: StatusCodes.Status403Forbidden);
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            using var reader = new StreamReader(request.Body);
            var markdown = await reader.ReadToEndAsync().ConfigureAwait(false);

            var validation = ValidateFrontmatter(markdown);
            if (validation is not null) return Results.BadRequest(new { error = validation });

            try
            {
                KnowledgePage? page;
                string path;

                switch (kind.ToLowerInvariant())
                {
                    case "skills":
                        page = BuildSkillPage(slug, markdown);
                        path = $"db://knowledge_pages/skills/{slug}";
                        break;
                    case "agents":
                        page = BuildAgentPage(slug, markdown);
                        path = $"db://knowledge_pages/agents/{slug}";
                        break;
                    case "documents":
                        docs.SaveGlobal(slug, markdown);
                        path = UserDocumentTemplateRegistry.GlobalPathFor(slug);
                        page = BuildDocPage(slug, docs);
                        break;
                    case "tools":
                        tools.SaveGlobal(slug, markdown);
                        path = UserToolTemplateRegistry.GlobalPathFor(slug);
                        page = BuildToolPage(slug, tools);
                        break;
                    case "guidelines":
                        page = ParseGuidelinePage(slug, markdown);
                        path = $"db://knowledge_pages/guidelines/{slug}";
                        break;
                    default:
                        return Results.NotFound(new { error = $"Unknown kind '{kind}'." });
                }

                if (page is not null)
                    await knowledgeStore.UpsertAsync(page).ConfigureAwait(false);

                return Results.Ok(new { slug, path });
            }
            catch (IOException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        // Delete user-tier entry.
        app.MapDelete("/v1/knowledge/{kind}/{slug}", async (
            string kind,
            string slug,
            HttpContext ctx,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools,
            IKnowledgeStore knowledgeStore) =>
        {
            if (!HttpContextAuthExtensions.IsAdmin(ctx))
                return Results.Json(new { error = "Admin role required to modify knowledge." }, statusCode: StatusCodes.Status403Forbidden);
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            var kindLower = kind.ToLowerInvariant();

            switch (kindLower)
            {
                case "guidelines":
                case "skills":
                case "agents":
                    // DB-only: delete the User-tier overlay; BuiltIn base remains.
                    await knowledgeStore.DeleteAsync(kindLower, slug, KnowledgeScope.Global).ConfigureAwait(false);
                    return Results.Ok(new { slug });

                case "documents":
                {
                    var deleted = docs.DeleteGlobal(slug);
                    if (!deleted) return Results.StatusCode(403);
                    await knowledgeStore.DeleteAsync(kindLower, slug, KnowledgeScope.Global).ConfigureAwait(false);
                    return Results.Ok(new { slug });
                }
                case "tools":
                {
                    var deleted = tools.DeleteGlobal(slug);
                    if (!deleted) return Results.StatusCode(403);
                    await knowledgeStore.DeleteAsync(kindLower, slug, KnowledgeScope.Global).ConfigureAwait(false);
                    return Results.Ok(new { slug });
                }
                default:
                    return Results.NotFound(new { error = $"Unknown kind '{kind}'." });
            }
        });
    }

    // ── Source readers ────────────────────────────────────────────────────────

    private static async Task<IResult> GetGuidelineSourceAsync(string slug, IKnowledgeStore store)
    {
        var dbPage = await store.GetActiveAsync("guidelines", slug).ConfigureAwait(false);
        if (dbPage is not null)
        {
            var markdown = $"---\nname: {dbPage.Name}\ndescription: {dbPage.Description}\n---\n\n{dbPage.Body}";
            return Results.Ok(new { slug, tier = dbPage.Tier, readOnly = false, content = markdown });
        }

        var builtin = LanguageGuidelines.Get(slug);
        if (builtin is null) return Results.NotFound(new { error = $"Guideline '{slug}' not found." });
        return Results.Ok(new { slug, tier = "BuiltIn", readOnly = false, content = LanguageGuidelines.ToMarkdown(builtin) });
    }

    private static async Task<IResult> GetDbSourceAsync(string kind, string slug, IKnowledgeStore store)
    {
        var page = await store.GetEffectiveAsync(kind, slug).ConfigureAwait(false);
        if (page is null) return Results.NotFound(new { error = $"{kind[..^1]} '{slug}' not found." });

        var content = kind == "agents"
            ? ReconstructAgentMarkdown(page)
            : ReconstructSkillMarkdown(page);

        var readOnly = page.Tier == "BuiltIn";
        return Results.Ok(new { slug, tier = page.Tier, readOnly, content });
    }

    private static IResult GetDocSource(string slug, UserDocumentTemplateRegistry docs)
    {
        var template = docs.TryGet(slug);
        if (template is null) return Results.NotFound(new { error = $"Document template '{slug}' not found." });
        try
        {
            var content = File.ReadAllText(template.SourcePath);
            return Results.Ok(new { slug, tier = template.Tier.ToString(), readOnly = template.Tier == UserTemplateTier.BuiltIn, content });
        }
        catch (IOException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    private static IResult GetToolSource(string slug, UserToolTemplateRegistry tools)
    {
        var template = tools.TryGet(slug);
        if (template is null) return Results.NotFound(new { error = $"Tool template '{slug}' not found." });
        try
        {
            var content = File.ReadAllText(template.SourcePath);
            return Results.Ok(new { slug, tier = template.Tier.ToString(), readOnly = template.Tier == UserToolTier.BuiltIn, content });
        }
        catch (IOException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    }

    // ── Markdown reconstruction ───────────────────────────────────────────────

    private static string ReconstructSkillMarkdown(KnowledgePage p)
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(string.Create(ic, $"name: {p.Name}"));
        if (!string.IsNullOrWhiteSpace(p.Description))
            sb.AppendLine(string.Create(ic, $"description: {p.Description}"));
        if (!string.IsNullOrWhiteSpace(p.Trigger))
            sb.AppendLine(string.Create(ic, $"trigger: {p.Trigger}"));
        if (!string.IsNullOrWhiteSpace(p.Agents))
        {
            var list = SafeDeserializeList(p.Agents);
            if (list.Count > 0) sb.AppendLine(string.Create(ic, $"agents: [{string.Join(", ", list)}]"));
        }
        if (!string.IsNullOrWhiteSpace(p.Tools))
        {
            var list = SafeDeserializeList(p.Tools);
            if (list.Count > 0) sb.AppendLine(string.Create(ic, $"tools: [{string.Join(", ", list)}]"));
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(p.Body);
        return sb.ToString();
    }

    private static string ReconstructAgentMarkdown(KnowledgePage p)
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(string.Create(ic, $"name: {p.Name}"));
        if (!string.IsNullOrWhiteSpace(p.Description))
            sb.AppendLine(string.Create(ic, $"description: {p.Description}"));
        if (!string.IsNullOrWhiteSpace(p.Role))
            sb.AppendLine(string.Create(ic, $"role: {p.Role}"));
        if (!string.IsNullOrWhiteSpace(p.RecommendedLevel))
            sb.AppendLine(string.Create(ic, $"recommended_level: {p.RecommendedLevel}"));
        if (!string.IsNullOrWhiteSpace(p.Tools))
        {
            var list = SafeDeserializeList(p.Tools);
            if (list.Count > 0) sb.AppendLine(string.Create(ic, $"allowed_tools: [{string.Join(", ", list)}]"));
        }
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(p.Body);
        return sb.ToString();
    }

    private static List<string> SafeDeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    // ── DB page builders ──────────────────────────────────────────────────────

    private static KnowledgePage BuildSkillPage(string slug, string markdown)
    {
        var now = DateTimeOffset.UtcNow;
        var (name, description, trigger, agents, tools, body) = ExtractSkillParts(slug, markdown);
        return new KnowledgePage(
            KnowledgeId:   $"skill_global_{slug}",
            Kind:          "skills",
            Slug:          slug,
            Name:          name,
            Description:   description,
            Tier:          "User",
            Body:          body,
            WorkspaceId:   KnowledgeScope.Global,
            CreatedAt:     now,
            UpdatedAt:     now,
            Trigger:       string.IsNullOrWhiteSpace(trigger) ? null : trigger,
            Agents:        agents,
            Tools:         tools);
    }

    private static KnowledgePage BuildAgentPage(string slug, string markdown)
    {
        var now = DateTimeOffset.UtcNow;
        var (name, description, role, level, tools, body) = ExtractAgentParts(slug, markdown);
        return new KnowledgePage(
            KnowledgeId:      $"agent_global_{slug}",
            Kind:             "agents",
            Slug:             slug,
            Name:             name,
            Description:      description,
            Tier:             "User",
            Body:             body,
            WorkspaceId:      KnowledgeScope.Global,
            CreatedAt:        now,
            UpdatedAt:        now,
            Tools:            tools,
            Role:             string.IsNullOrWhiteSpace(role) ? null : role,
            RecommendedLevel: string.IsNullOrWhiteSpace(level) ? null : level);
    }

    private static KnowledgePage? BuildDocPage(string slug, UserDocumentTemplateRegistry docs)
    {
        var t = docs.TryGet(slug);
        if (t is null) return null;
        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage(
            KnowledgeId:   $"doc_{slug}",
            Kind:          "documents",
            Slug:          slug,
            Name:          t.Name,
            Description:   t.Description,
            Tier:          "User",
            Body:          t.Body,
            WorkspaceId:   KnowledgeScope.Global,
            CreatedAt:     now,
            UpdatedAt:     now,
            Industry:      t.Industry,
            DefaultFormat: t.DefaultFormat);
    }

    private static KnowledgePage? BuildToolPage(string slug, UserToolTemplateRegistry tools)
    {
        var t = tools.TryGet(slug);
        if (t is null) return null;
        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage(
            KnowledgeId:   $"tool_{slug}",
            Kind:          "tools",
            Slug:          slug,
            Name:          t.Name,
            Description:   t.Description,
            Tier:          "User",
            Body:          t.Body,
            WorkspaceId:   KnowledgeScope.Global,
            CreatedAt:     now,
            UpdatedAt:     now,
            Category:      t.Category);
    }

    private static KnowledgePage ParseGuidelinePage(string slug, string markdown)
    {
        var now = DateTimeOffset.UtcNow;
        var (name, description, body) = ExtractGuidelineParts(slug, markdown);
        return new KnowledgePage(
            KnowledgeId: $"guideline_{slug}",
            Kind: "guidelines",
            Slug: slug,
            Name: name,
            Description: description,
            Tier: "User",
            Body: body,
            WorkspaceId: "",
            CreatedAt: now,
            UpdatedAt: now);
    }

    // ── Frontmatter parsers ───────────────────────────────────────────────────

    private static (string Name, string Description, string Trigger, string? Agents, string? Tools, string Body)
        ExtractSkillParts(string slug, string markdown)
    {
        var (meta, body) = ParseFrontmatter(markdown);
        meta.TryGetValue("name", out var name);
        meta.TryGetValue("description", out var description);
        meta.TryGetValue("trigger", out var trigger);
        string? agentsJson = null, toolsJson = null;
        if (meta.TryGetValue("agents", out var agentsRaw))
        {
            var list = ParseList(agentsRaw);
            if (list.Count > 0) agentsJson = JsonSerializer.Serialize(list);
        }
        if (meta.TryGetValue("tools", out var toolsRaw))
        {
            var list = ParseList(toolsRaw);
            if (list.Count > 0) toolsJson = JsonSerializer.Serialize(list);
        }
        return (string.IsNullOrWhiteSpace(name) ? slug : name.Trim(),
                description?.Trim() ?? string.Empty,
                trigger?.Trim() ?? string.Empty,
                agentsJson, toolsJson,
                body);
    }

    private static (string Name, string Description, string Role, string Level, string? Tools, string Body)
        ExtractAgentParts(string slug, string markdown)
    {
        var (meta, body) = ParseFrontmatter(markdown);
        meta.TryGetValue("name", out var name);
        meta.TryGetValue("description", out var description);
        meta.TryGetValue("role", out var role);
        meta.TryGetValue("recommended_level", out var level);
        string? toolsJson = null;
        if (meta.TryGetValue("allowed_tools", out var toolsRaw))
        {
            var list = ParseList(toolsRaw);
            if (list.Count > 0) toolsJson = JsonSerializer.Serialize(list);
        }
        return (string.IsNullOrWhiteSpace(name) ? slug : name.Trim(),
                description?.Trim() ?? string.Empty,
                role?.Trim() ?? string.Empty,
                level?.Trim() ?? string.Empty,
                toolsJson, body);
    }

    private static (string Name, string Description, string Body) ExtractGuidelineParts(string slug, string markdown)
    {
        var (meta, body) = ParseFrontmatter(markdown);
        meta.TryGetValue("name", out var name);
        meta.TryGetValue("description", out var description);
        return (string.IsNullOrWhiteSpace(name) ? slug : name.Trim().Trim('"', '\''),
                description?.Trim().Trim('"', '\'') ?? string.Empty,
                body);
    }

    private static (Dictionary<string, string> Meta, string Body) ParseFrontmatter(string markdown)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var body = markdown;

        if (!markdown.StartsWith("---", StringComparison.Ordinal))
            return (meta, body);

        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return (meta, body);

        var fm = markdown.Substring(3, end - 3);
        body = markdown[(end + 4)..].TrimStart();

        foreach (var line in fm.Split('\n'))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            meta[key] = value;
        }
        return (meta, body);
    }

    private static IReadOnlyList<string> ParseList(string value)
    {
        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']')) v = v[1..^1];
        return [.. v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    private static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 80) return false;
        foreach (var c in slug)
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        return true;
    }

    private static string? ValidateFrontmatter(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "Body cannot be empty.";
        if (!markdown.StartsWith("---", StringComparison.Ordinal)) return "Missing YAML frontmatter (must start with ---)";
        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return "Frontmatter is not closed (missing trailing ---)";
        var fm = markdown.Substring(3, end - 3);
        foreach (var line in fm.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var v = t[5..].Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(v)) return null;
            }
        }
        return "Frontmatter must include a non-empty 'name' field.";
    }
}
