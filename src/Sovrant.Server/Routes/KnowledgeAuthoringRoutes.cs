using Sovrant.Runtime.Documents.Templates;
using Sovrant.Tools.Skills;
using Sovrant.Tools.Templates;

namespace Sovrant.Server.Routes;

/// <summary>
/// Registers authoring endpoints under <c>/v1/knowledge/{kind}/{slug}</c> for the
/// Knowledge UI's markdown editor. <c>kind</c> ∈ <c>documents | tools | skills</c>.
/// Built-in (assembly-shipped) entries are read-only — saves and deletes require user-tier or new slugs.
/// </summary>
internal static class KnowledgeAuthoringRoutes
{
    public static void Map(WebApplication app)
    {
        // Read source markdown for editor pre-load
        app.MapGet("/v1/knowledge/{kind}/{slug}/source", (
            string kind,
            string slug,
            SkillRegistry skills,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools) =>
        {
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            return kind.ToUpperInvariant() switch
            {
                "SKILLS" => GetSkillSource(slug, skills),
                "DOCUMENTS" => GetDocSource(slug, docs),
                "TOOLS" => GetToolSource(slug, tools),
                _ => Results.NotFound(new { error = $"Unknown kind '{kind}'." }),
            };
        });

        // Save markdown to user (global) tier
        app.MapPost("/v1/knowledge/{kind}/{slug}", async (
            string kind,
            string slug,
            HttpRequest request,
            SkillRegistry skills,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools) =>
        {
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            using var reader = new StreamReader(request.Body);
            var markdown = await reader.ReadToEndAsync().ConfigureAwait(false);

            var validation = ValidateFrontmatter(markdown);
            if (validation is not null) return Results.BadRequest(new { error = validation });

            try
            {
                switch (kind.ToUpperInvariant())
                {
                    case "SKILLS":
                        skills.SaveGlobal(slug, markdown);
                        return Results.Ok(new { slug, path = SkillRegistry.GlobalPathFor(slug) });
                    case "DOCUMENTS":
                        docs.SaveGlobal(slug, markdown);
                        return Results.Ok(new { slug, path = UserDocumentTemplateRegistry.GlobalPathFor(slug) });
                    case "TOOLS":
                        tools.SaveGlobal(slug, markdown);
                        return Results.Ok(new { slug, path = UserToolTemplateRegistry.GlobalPathFor(slug) });
                    default:
                        return Results.NotFound(new { error = $"Unknown kind '{kind}'." });
                }
            }
            catch (IOException ex)
            {
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        // Delete user-tier file (built-ins remain readable; 403 if no user-tier file exists)
        app.MapDelete("/v1/knowledge/{kind}/{slug}", (
            string kind,
            string slug,
            SkillRegistry skills,
            UserDocumentTemplateRegistry docs,
            UserToolTemplateRegistry tools) =>
        {
            if (!IsValidSlug(slug)) return Results.BadRequest(new { error = "Invalid slug." });

            var deleted = kind.ToUpperInvariant() switch
            {
                "SKILLS" => skills.DeleteGlobal(slug),
                "DOCUMENTS" => docs.DeleteGlobal(slug),
                "TOOLS" => tools.DeleteGlobal(slug),
                _ => (bool?)null,
            };
            if (deleted is null) return Results.NotFound(new { error = $"Unknown kind '{kind}'." });
            if (deleted == false) return Results.StatusCode(403);
            return Results.Ok(new { slug });
        });
    }

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
