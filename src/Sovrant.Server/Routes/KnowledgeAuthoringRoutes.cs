using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Knowledge;
using Sovrant.Server.Auth;
using Sovrant.Tools.Skills;
using Sovrant.Tools.Templates;
using System.Text.Json;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers authoring endpoints under <c>/v1/knowledge/{kind}/{slug}</c> for the
/// Knowledge UI's markdown editor. <c>kind</c> ∈ <c>documents | tools | skills</c>.
/// Built-in (assembly-shipped) entries are read-only — saves and deletes require user-tier or new slugs.
/// Writes are dual-pathed: filesystem (registry) + DB (IKnowledgeStore) for Phase 108 readiness.
/// </summary>
internal static class KnowledgeAuthoringRoutes
{
    public static void Map(WebApplication app)
    {
        // Read source markdown for editor pre-load
        app.MapGet("/v1/knowledge/{kind}/{slug}/source", async (
            string kind,
            string slug,
            SkillRegistry skills,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools,
            IKnowledgeStore knowledgeStore) =>
        {
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            if (kind.Equals("guidelines", StringComparison.OrdinalIgnoreCase))
                return await GetGuidelineSourceAsync(slug, knowledgeStore).ConfigureAwait(false);

            return kind.ToUpperInvariant() switch
            {
                "SKILLS" => GetSkillSource(slug, skills),
                "DOCUMENTS" => GetDocSource(slug, docs),
                "TOOLS" => GetToolSource(slug, tools),
                _ => Results.NotFound(new { error = $"Unknown kind '{kind}'." }),
            };
        });

        // Save markdown to user (global) tier — filesystem + DB dual-write.
        app.MapPost("/v1/knowledge/{kind}/{slug}", async (
            string kind,
            string slug,
            HttpRequest request,
            HttpContext ctx,
            SkillRegistry skills,
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
                var kindKey = kind.ToUpperInvariant();
                string? path;
                KnowledgePage? page;

                switch (kindKey)
                {
                    case "SKILLS":
                        skills.SaveGlobal(slug, markdown);
                        path = SkillRegistry.GlobalPathFor(slug);
                        page = BuildSkillPage(slug, skills);
                        break;
                    case "DOCUMENTS":
                        docs.SaveGlobal(slug, markdown);
                        path = UserDocumentTemplateRegistry.GlobalPathFor(slug);
                        page = BuildDocPage(slug, docs);
                        break;
                    case "TOOLS":
                        tools.SaveGlobal(slug, markdown);
                        path = UserToolTemplateRegistry.GlobalPathFor(slug);
                        page = BuildToolPage(slug, tools);
                        break;
                    case "GUIDELINES":
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

        // Delete user-tier file (built-ins remain readable; 403 if no user-tier file exists).
        // Also removes the DB record so the two stores stay in sync.
        app.MapDelete("/v1/knowledge/{kind}/{slug}", async (
            string kind,
            string slug,
            HttpContext ctx,
            SkillRegistry skills,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools,
            IKnowledgeStore knowledgeStore) =>
        {
            if (!HttpContextAuthExtensions.IsAdmin(ctx))
                return Results.Json(new { error = "Admin role required to modify knowledge." }, statusCode: StatusCodes.Status403Forbidden);
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            var kindLower = kind.ToLowerInvariant();

            if (kindLower == "guidelines")
            {
                // Guidelines are DB-only; delete the user override (reverts to built-in).
                await knowledgeStore.DeleteAsync("guidelines", slug).ConfigureAwait(false);
                return Results.Ok(new { slug });
            }

            var deleted = kind.ToUpperInvariant() switch
            {
                "SKILLS" => skills.DeleteGlobal(slug),
                "DOCUMENTS" => docs.DeleteGlobal(slug),
                "TOOLS" => tools.DeleteGlobal(slug),
                _ => (bool?)null,
            };
            if (deleted is null) return Results.NotFound(new { error = $"Unknown kind '{kind}'." });
            if (deleted == false) return Results.StatusCode(403);

            await knowledgeStore.DeleteAsync(kindLower, slug).ConfigureAwait(false);
            return Results.Ok(new { slug });
        });
    }

    // ── Guidelines (DB-only, no filesystem registry) ─────────────────────────

    private static async Task<IResult> GetGuidelineSourceAsync(string slug, IKnowledgeStore store)
    {
        // User override in DB wins; fall back to static built-in.
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

    private static KnowledgePage ParseGuidelinePage(string slug, string markdown)
    {
        var now = DateTimeOffset.UtcNow;
        // Extract name and description from frontmatter; body is everything after.
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

    private static (string Name, string Description, string Body) ExtractGuidelineParts(string slug, string markdown)
    {
        var name = slug;
        var description = string.Empty;
        var body = markdown;

        if (markdown.StartsWith("---", StringComparison.Ordinal))
        {
            var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end >= 0)
            {
                var fm = markdown.Substring(3, end - 3);
                body = markdown[(end + 4)..].TrimStart();
                foreach (var line in fm.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                        name = t[5..].Trim().Trim('"', '\'');
                    else if (t.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                        description = t[12..].Trim().Trim('"', '\'');
                }
            }
        }
        return (name, description, body);
    }

    // ── DB page builders (called after SaveGlobal so the registry is up to date) ──

    private static KnowledgePage? BuildSkillPage(string slug, SkillRegistry skills)
    {
        var skill = skills.TryGetByName(slug);
        if (skill is null) return null;

        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage(
            KnowledgeId:   $"skill_{slug}",
            Kind:          "skills",
            Slug:          slug,
            Name:          skill.Name,
            Description:   skill.Description,
            Tier:          "User",
            Body:          skill.Body,
            WorkspaceId:   "",
            CreatedAt:     now,
            UpdatedAt:     now,
            Trigger:       string.IsNullOrEmpty(skill.Trigger) ? null : skill.Trigger,
            Agents:        skill.Agents.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(skill.Agents) : null,
            Tools:         skill.Tools.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(skill.Tools) : null);
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
            WorkspaceId:   "",
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
            WorkspaceId:   "",
            CreatedAt:     now,
            UpdatedAt:     now,
            Category:      t.Category);
    }

    // ── Source readers ─────────────────────────────────────────────────────

    private static IResult GetSkillSource(string slug, SkillRegistry skills)
    {
        // Try by slug-as-name first; fall back to source path lookup by file basename.
        var skill = skills.TryGetByName(slug);
        if (skill is null)
            return Results.NotFound(new { error = $"Skill '{slug}' not found." });

        var source = skills.TryGetSource(skill.Name);
        if (source is null) return Results.NotFound(new { error = "Skill has no on-disk source." });

        try
        {
            var content = File.ReadAllText(source.Path);
            return Results.Ok(new { slug, tier = source.Tier.ToString(), readOnly = source.Tier == SkillTier.BuiltIn, content });
        }
        catch (IOException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
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

    /// <summary>Slugs must be alphanumeric + dash/underscore — keep filesystem writes safe.</summary>
    private static bool IsValidSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return false;
        if (slug.Length > 80) return false;
        foreach (var c in slug)
        {
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
        }
        return true;
    }

    /// <summary>Returns null if frontmatter is well-formed and contains a non-empty <c>name</c>; otherwise an error message.</summary>
    private static string? ValidateFrontmatter(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return "Body cannot be empty.";
        if (!markdown.StartsWith("---", StringComparison.Ordinal))
            return "Missing YAML frontmatter (must start with ---).";
        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0) return "Frontmatter is not closed (missing trailing ---).";

        var fm = markdown.Substring(3, end - 3);
        foreach (var line in fm.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed[5..].Trim().Trim('"', '\'');
                if (!string.IsNullOrEmpty(value)) return null;
            }
        }
        return "Frontmatter must include a non-empty 'name' field.";
    }
}
