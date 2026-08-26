using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Knowledge;
using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Workspaces;
using Sovrant.Tools.Projects.Scaffolds;

namespace Sovrant.Tools.Projects;

/// <summary>
/// Phase 73 — Scaffolds a new project from a registered <see cref="IProjectTemplate"/>.
/// Resolves the template by exact ID or by language + kind hint, runs the scaffold,
/// and writes every generated file into the current run-scoped artifact store.
/// </summary>
public sealed class CodeCreateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("CodeCreate", CreateSchema())
    {
        Description =
            "Scaffold a new code project from a built-in template. " +
            "Provide either 'template_id' (e.g. 'node/express-api', 'dotnet/webapi', 'python/fastapi') " +
            "or a 'language' + optional 'kind' hint to auto-select the best template. " +
            "Requires 'project_name' and 'run_id'. All generated files are written to the artifact store. " +
            "The response includes 'build_command', 'run_command', 'test_command', and a 'next_steps' list " +
            "ready to share with the user. Use 'CodeListTemplates' to discover available templates.",
    };

    private readonly ProjectTemplateRegistry _registry;
    private readonly IArtifactStore _store;
    private readonly IKnowledgeStore? _knowledgeStore;

    public CodeCreateTool(ProjectTemplateRegistry registry, IArtifactStore store, IKnowledgeStore? knowledgeStore = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _knowledgeStore = knowledgeStore;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var projectName = input.GetStringProp("project_name");
        if (string.IsNullOrWhiteSpace(projectName))
            return "Error: 'project_name' is required.";

        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required — scaffolded files are written into a run-scoped artifact.";

        var template = ResolveTemplate(input, out var resolveError);
        if (template is null)
            return resolveError!;

        // Build scaffold context
        Dictionary<string, string>? extras = null;
        if (input.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
        {
            extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in paramsEl.EnumerateObject())
            {
                var val = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
                extras[prop.Name] = val;
            }
        }

        var context = new ScaffoldContext(projectName, extras);

        IReadOnlyList<ProjectFile> files;
        try
        {
            files = template.Scaffold(context);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return $"Error: scaffold failed — {ex.Message}";
        }

        if (files.Count == 0)
            return $"Error: template '{template.Id}' produced no files for project '{projectName}'.";

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
            ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
            RunId = runId,
        };

        var written = new List<object>();
        var errors = new List<object>();

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);

            foreach (var file in files)
            {
                var filePath = $"{projectName}/{file.RelativePath}";
                var contentType = InferContentType(file.RelativePath);
                var bytes = Encoding.UTF8.GetBytes(file.Content);

                try
                {
                    using var stream = new MemoryStream(bytes);
                    await _store.WriteAsync(handle, filePath, stream, contentType, ct).ConfigureAwait(false);
                    var accessUrl = await _store.GetAccessUrlAsync(handle, filePath, TimeSpan.FromDays(1), ct).ConfigureAwait(false);
                    written.Add(new
                    {
                        path = filePath,
                        size_bytes = bytes.Length,
                        is_executable = file.IsExecutable,
                        access_url = accessUrl?.ToString(),
                    });
                }
                catch (ArgumentException ex)
                {
                    errors.Add(new { path = filePath, error = ex.Message });
                }
            }
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        var codeManifest = BuildCodeManifest(template, projectName);

        // Write code metadata into the run manifest so the Artifacts page and
        // downstream tools can surface build/run/test commands without re-reading files.
        try
        {
            var handle2 = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            await _store.SetCodeMetadataAsync(handle2, codeManifest, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ _ = ex; }

        // Resolve active language guideline (user override > built-in) and surface
        // it to the agent so it can apply conventions when writing additional code.
        string? conventions = null;
        if (_knowledgeStore is not null)
        {
            var dbGuideline = await _knowledgeStore.GetActiveAsync("guidelines", template.Language, ct).ConfigureAwait(false);
            conventions = dbGuideline?.Body;
        }
        conventions ??= LanguageGuidelines.Get(template.Language)?.Body;

        var nextSteps = BuildNextSteps(codeManifest, projectName);

        return JsonSerializer.Serialize(new
        {
            status = errors.Count == 0 ? "created" : "partial",
            template_id = template.Id,
            template_name = template.Name,
            project_name = projectName,
            file_count = written.Count,
            error_count = errors.Count,
            run_id = scope.RunId,
            workspace_id = scope.WorkspaceId,
            project_id = scope.ProjectId,
            build_command = codeManifest.BuildCommand,
            run_command = codeManifest.RunCommand,
            test_command = codeManifest.TestCommand,
            next_steps = nextSteps,
            files = written,
            errors,
            conventions,
        });
    }

    private IProjectTemplate? ResolveTemplate(JsonElement input, out string? error)
    {
        var templateId = input.GetStringProp("template_id");
        var language = input.GetStringProp("language");
        var kind = input.GetStringProp("kind");

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            // Exact ID or language/kind pass-through via Find
            var t = _registry.TryGet(templateId) ?? _registry.Find(templateId, kind);
            if (t is null)
            {
                error = $"Error: unknown template '{templateId}'. Use CodeListTemplates to see available templates.";
                return null;
            }
            error = null;
            return t;
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            var t = _registry.Find(language, kind);
            if (t is null)
            {
                error = $"Error: no template found for language '{language}'" +
                    (string.IsNullOrWhiteSpace(kind) ? "." : $" with kind '{kind}'.");
                return null;
            }
            error = null;
            return t;
        }

        error = "Error: either 'template_id' or 'language' is required.";
        return null;
    }

    private static CodeManifest BuildCodeManifest(IProjectTemplate template, string projectName)
    {
        var (defaultBuild, defaultRun, defaultTest) = ScaffoldCommands.For(template.Language, template.Kind, projectName);
        return new CodeManifest
        {
            TemplateId = template.Id,
            Language = template.Language,
            Kind = template.Kind,
            BuildCommand = template.BuildCommand ?? defaultBuild,
            RunCommand = template.RunCommand ?? defaultRun,
            TestCommand = template.TestCommand ?? defaultTest,
            EntryPoint = template.EntryPoint ?? ScaffoldCommands.EntryPoint(template.Language, projectName),
        };
    }

    private static string[] BuildNextSteps(CodeManifest manifest, string projectName)
    {
        var steps = new List<string>();
        if (manifest.BuildCommand is not null)
            steps.Add($"Build: {manifest.BuildCommand}");
        if (manifest.RunCommand is not null)
            steps.Add($"Run: {manifest.RunCommand}");
        if (manifest.TestCommand is not null)
            steps.Add($"Test: {manifest.TestCommand}");
        steps.Add($"See {projectName}/README.md for full setup instructions");
        return [.. steps];
    }

    private static string InferContentType(string path)
    {
        var ext = Path.GetExtension(path).ToUpperInvariant();
        return ext switch
        {
            ".TS" or ".TSX" or ".JS" or ".MJS" or ".CJS" => "text/javascript",
            ".JSON" or ".JSONC" => "application/json",
            ".MD" => "text/markdown",
            ".TOML" or ".ZON" => "text/plain",
            ".YAML" or ".YML" => "application/yaml",
            ".CS" => "text/x-csharp",
            ".PY" => "text/x-python",
            ".GO" => "text/x-go",
            ".RS" => "text/x-rust",
            ".JAVA" => "text/x-java",
            ".KT" => "text/x-kotlin",
            ".RB" => "text/x-ruby",
            ".SWIFT" => "text/x-swift",
            ".LUA" => "text/x-lua",
            ".ZIG" => "text/x-zig",
            ".XML" => "application/xml",
            ".HTML" or ".HTM" => "text/html",
            ".CSS" => "text/css",
            ".SH" or ".BASH" => "text/x-shellscript",
            ".GITIGNORE" or ".ENV" or ".EDITORCONFIG" => "text/plain",
            _ => "text/plain",
        };
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "template_id": {
                    "type": "string",
                    "description": "Exact template ID (e.g. 'node/express-api', 'dotnet/webapi', 'python/fastapi', 'go/api', 'rust/cli'). Use CodeListTemplates to see all IDs."
                },
                "language": {
                    "type": "string",
                    "description": "Language name (e.g. 'node', 'dotnet', 'python', 'go', 'rust', 'java', 'kotlin', 'ruby', 'swift', 'lua', 'zig'). Used when template_id is omitted."
                },
                "kind": {
                    "type": "string",
                    "description": "Optional kind hint within a language group (e.g. 'cli', 'webapi', 'library', 'script'). Helps select the right template when multiple exist for a language."
                },
                "project_name": {
                    "type": "string",
                    "description": "The project name. Used to derive package names, class names, and directory structure inside the scaffold."
                },
                "parameters": {
                    "type": "object",
                    "description": "Optional template-specific parameters as a key-value map. Check the template's declared parameters via CodeListTemplates."
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID that scopes the artifact. Required."
                },
                "workspace_id": {
                    "type": "string",
                    "description": "Workspace ID (defaults to 'personal')."
                },
                "project_id": {
                    "type": "string",
                    "description": "Project ID (defaults to 'default-project')."
                }
            },
            "required": ["project_name", "run_id"]
        }
        """).RootElement;
}
