using System.Globalization;
using System.Text;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Phase 73 — CLI command for scaffolding new code projects.
/// <c>/project new &lt;language&gt; &lt;project-name&gt; [--kind &lt;kind&gt;]</c>
/// <c>/project templates [language]</c>
/// </summary>
public sealed class ProjectCommand : ISlashCommand
{
    private readonly ProjectTemplateRegistry _registry;
    private readonly IArtifactStore _store;

    public ProjectCommand(ProjectTemplateRegistry registry, IArtifactStore store)
    {
        _registry = registry;
        _store = store;
    }

    public string Name => "project";
    public IReadOnlyList<string> Aliases => ["proj"];
    public string Description => "Scaffold a new code project from a built-in template.";
    public string Category => "Code";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Help();

        return parts[0].ToUpperInvariant() switch
        {
            "NEW" or "CREATE" => await NewAsync(parts[1..], ct).ConfigureAwait(false),
            "TEMPLATES" or "LIST" => ListTemplates(parts[1..]),
            "HELP" => Help(),
            _ => await NewAsync(parts, ct).ConfigureAwait(false),
        };
    }

    // /project new <language-or-id> <project-name> [--kind <kind>]
    private async Task<SlashCommandResult> NewAsync(string[] parts, CancellationToken ct)
    {
        if (parts.Length < 2)
            return Err("usage: /project new <language-or-template-id> <project-name> [--kind <kind>]");

        var languageOrId = parts[0];
        var kind = ParseFlag(parts[1..], "--kind");
        var projectName = parts[1].StartsWith("--", StringComparison.Ordinal) ? (parts.Length > 3 ? parts[3] : null) : parts[1];

        // Handle: /project new node/express-api my-app
        // Handle: /project new node my-app --kind express-api
        // Rebuild: strip flags to get the project name
        var positional = parts.Where(p => !p.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (positional.Length < 2)
            return Err("usage: /project new <language-or-template-id> <project-name> [--kind <kind>]");

        languageOrId = positional[0];
        projectName = string.Join(' ', positional[1..]);

        var template = _registry.TryGet(languageOrId) ?? _registry.Find(languageOrId, kind);
        if (template is null)
        {
            var languages = string.Join(", ", _registry.Languages.OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
            return Err($"No template found for '{languageOrId}'" +
                (kind is not null ? $" with kind '{kind}'" : string.Empty) +
                $". Available languages: {languages}");
        }

        var context = new ScaffoldContext(projectName);
        IReadOnlyList<ProjectFile> files;
        try
        {
            files = template.Scaffold(context);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Err($"Scaffold failed: {ex.Message}");
        }

        var runId = $"proj-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
        var scope = new ArtifactScope
        {
            WorkspaceId = WorkspaceIdentity.DefaultPersonal(),
            ProjectId = ArtifactScope.DefaultProjectId,
            RunId = runId,
        };

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            var written = new List<string>();

            foreach (var file in files)
            {
                var path = $"{projectName}/{file.RelativePath}";
                var bytes = System.Text.Encoding.UTF8.GetBytes(file.Content);
                using var stream = new System.IO.MemoryStream(bytes);
                await _store.WriteAsync(handle, path, stream, "text/plain", ct).ConfigureAwait(false);
                written.Add(path);
            }

            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Scaffolded **{projectName}** using template `{template.Id}` ({files.Count} files)");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Run ID: `{runId}`");
            sb.AppendLine();
            foreach (var p in written)
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {p}");
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Use `/artifacts open {runId}` to open the output folder.");

            return new SlashCommandResult(sb.ToString());
        }
        catch (ArgumentException ex)
        {
            return Err($"Failed to write artifacts: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Err($"Failed to write artifacts: {ex.Message}");
        }
    }

    // /project templates [language]
    private SlashCommandResult ListTemplates(string[] parts)
    {
        var language = parts.FirstOrDefault(p => !p.StartsWith("--", StringComparison.Ordinal));

        IEnumerable<IProjectTemplate> templates = string.IsNullOrWhiteSpace(language)
            ? _registry.All.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            : _registry.ForLanguage(language).OrderBy(t => t.Kind, StringComparer.OrdinalIgnoreCase);

        var list = templates.ToList();
        if (list.Count == 0)
            return new SlashCommandResult(string.IsNullOrWhiteSpace(language)
                ? "No templates registered."
                : $"No templates registered for language '{language}'.");

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(language))
            sb.AppendLine(CultureInfo.InvariantCulture, $"Templates for **{language}**:");
        else
            sb.AppendLine(CultureInfo.InvariantCulture, $"**{list.Count} project templates** (use `/project new <id> <name>` to scaffold):");
        sb.AppendLine();

        var byLang = list.GroupBy(t => t.Language, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byLang)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**{group.Key}**");
            foreach (var t in group.OrderBy(t => t.Kind, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine(CultureInfo.InvariantCulture, $"  `{t.Id}` — {t.Description}");
            sb.AppendLine();
        }

        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private static SlashCommandResult Help() => new("""
        **Usage:**
          /project new <language-or-id> <project-name> [--kind <kind>]
          /project templates [language]

        **Examples:**
          /project new node my-api --kind express-api
          /project new dotnet/webapi MyService
          /project new python fastapi-demo
          /project templates
          /project templates rust
        """);

    private static SlashCommandResult Err(string msg) => new($"Error: {msg}");

    private static string? ParseFlag(string[] parts, string flag)
    {
        for (var i = 0; i < parts.Length - 1; i++)
            if (string.Equals(parts[i], flag, StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        return null;
    }
}
