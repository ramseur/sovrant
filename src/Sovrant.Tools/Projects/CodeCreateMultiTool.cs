using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Projects.Templates;
using Sovrant.Runtime.Workspaces;
using Sovrant.Tools.Projects.Scaffolds;

namespace Sovrant.Tools.Projects;

/// <summary>
/// Phase 73 — Scaffolds multiple interdependent project components in a single call.
/// Each component is resolved by template ID or language/kind, scaffolded independently,
/// then written side-by-side under a shared monorepo root in the artifact store.
/// </summary>
public sealed class CodeCreateMultiTool : ITool
{
    private static readonly ToolDefinition s_definition = new("CodeCreateMulti", CreateSchema())
    {
        Description =
            "Scaffold multiple interdependent project components (e.g. a monorepo with an API + shared library + frontend) " +
            "in a single call. Each entry in 'components' maps to a template, producing files under " +
            "'<root_name>/<project_name>/...' in the artifact store. Each component in the response includes " +
            "'build_command', 'run_command', and 'test_command'. A 'next_steps' list is also returned. " +
            "Use 'CodeListTemplates' to discover available template IDs.",
    };

    private readonly ProjectTemplateRegistry _registry;
    private readonly IArtifactStore _store;

    public CodeCreateMultiTool(ProjectTemplateRegistry registry, IArtifactStore store)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var rootName = input.GetStringProp("root_name");
        if (string.IsNullOrWhiteSpace(rootName))
            return "Error: 'root_name' is required — it becomes the top-level directory for all components.";

        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required.";

        if (!input.TryGetProperty("components", out var componentsEl) ||
            componentsEl.ValueKind != JsonValueKind.Array)
            return "Error: 'components' must be a non-empty array.";

        var componentSpecs = componentsEl.EnumerateArray().ToList();
        if (componentSpecs.Count == 0)
            return "Error: 'components' array is empty.";

        // Resolve all templates before writing anything.
        var resolved = new List<(string projectName, IProjectTemplate template, ScaffoldContext context)>();
        foreach (var spec in componentSpecs)
        {
            var projectName = spec.GetStringProp("project_name");
            if (string.IsNullOrWhiteSpace(projectName))
                return "Error: each component must have a 'project_name'.";

            var template = ResolveTemplate(spec, out var resolveError);
            if (template is null)
                return $"Error in component '{projectName}': {resolveError}";

            Dictionary<string, string>? extras = null;
            if (spec.TryGetProperty("parameters", out var paramsEl) &&
                paramsEl.ValueKind == JsonValueKind.Object)
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

            resolved.Add((projectName, template, new ScaffoldContext(projectName, extras)));
        }

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
            ProjectId = input.GetStringProp("project_id", ArtifactScope.DefaultProjectId),
            RunId = runId,
        };

        var componentResults = new List<object>();
        var allErrors = new List<object>();

        try
        {
            var handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);

            foreach (var (projectName, template, context) in resolved)
            {
                IReadOnlyList<Runtime.Projects.Templates.ProjectFile> files;
                try
                {
                    files = template.Scaffold(context);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    allErrors.Add(new { component = projectName, error = ex.Message });
                    continue;
                }

                var written = new List<object>();
                var componentErrors = new List<object>();

                foreach (var file in files)
                {
                    var filePath = $"{rootName}/{projectName}/{file.RelativePath}";
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
                        componentErrors.Add(new { path = filePath, error = ex.Message });
                    }
                }

                allErrors.AddRange(componentErrors);
                var (compBuild, compRun, compTest) = ScaffoldCommands.For(template.Language, template.Kind, projectName);
                componentResults.Add(new
                {
                    component = projectName,
                    template_id = template.Id,
                    template_name = template.Name,
                    file_count = written.Count,
                    error_count = componentErrors.Count,
                    build_command = template.BuildCommand ?? compBuild,
                    run_command = template.RunCommand ?? compRun,
                    test_command = template.TestCommand ?? compTest,
                    files = written,
                });
            }
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        // Write a top-level code manifest. For monorepos the commands are
        // too variable to derive reliably, so we record the template list and
        // mark the kind as "monorepo" for discoverability.
        try
        {
            var manifestHandle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
            var languages = resolved.Select(r => r.template.Language).Distinct().ToList();
            var templateIds = resolved.Select(r => r.template.Id).ToList();
            await _store.SetCodeMetadataAsync(manifestHandle, new CodeManifest
            {
                TemplateId = templateIds.Count == 1 ? templateIds[0] : string.Join(", ", templateIds),
                Language = languages.Count == 1 ? languages[0] : string.Join(", ", languages),
                Kind = "monorepo",
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { /* best effort */ _ = ex; }

        var nextSteps = BuildNextSteps(rootName, resolved);

        return JsonSerializer.Serialize(new
        {
            status = allErrors.Count == 0 ? "created" : "partial",
            root_name = rootName,
            component_count = componentResults.Count,
            total_file_count = componentResults.Sum(c => (int)((dynamic)c).file_count),
            error_count = allErrors.Count,
            run_id = scope.RunId,
            workspace_id = scope.WorkspaceId,
            project_id = scope.ProjectId,
            next_steps = nextSteps,
            components = componentResults,
            errors = allErrors,
        });
    }

    private static string[] BuildNextSteps(
        string rootName,
        IReadOnlyList<(string projectName, IProjectTemplate template, ScaffoldContext context)> components)
    {
        var steps = new List<string>();
        foreach (var (projectName, template, _) in components)
        {
            var (build, run, test) = ScaffoldCommands.For(template.Language, template.Kind, projectName);
            var buildCmd = template.BuildCommand ?? build;
            var runCmd = template.RunCommand ?? run;
            var testCmd = template.TestCommand ?? test;
            if (buildCmd is not null) steps.Add($"Build {projectName}: {buildCmd}");
            if (runCmd is not null) steps.Add($"Run {projectName}: {runCmd}");
            if (testCmd is not null) steps.Add($"Test {projectName}: {testCmd}");
        }
        steps.Add($"See {rootName}/*/README.md files for full setup instructions per component");
        return [.. steps];
    }

    private IProjectTemplate? ResolveTemplate(JsonElement spec, out string? error)
    {
        var templateId = spec.GetStringProp("template_id");
        var language = spec.GetStringProp("language");
        var kind = spec.GetStringProp("kind");

        if (!string.IsNullOrWhiteSpace(templateId))
        {
            var t = _registry.TryGet(templateId) ?? _registry.Find(templateId, kind);
            if (t is null)
            {
                error = $"unknown template '{templateId}'. Use CodeListTemplates to see available templates.";
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
                error = $"no template found for language '{language}'" +
                    (string.IsNullOrWhiteSpace(kind) ? "." : $" with kind '{kind}'.");
                return null;
            }
            error = null;
            return t;
        }

        error = "either 'template_id' or 'language' is required.";
        return null;
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
            _ => "text/plain",
        };
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "root_name": {
                    "type": "string",
                    "description": "Top-level directory name for the multi-component project (e.g. 'my-platform'). All components are written under '<root_name>/<project_name>/'."
                },
                "components": {
                    "type": "array",
                    "description": "One entry per project component to scaffold.",
                    "items": {
                        "type": "object",
                        "properties": {
                            "project_name": {
                                "type": "string",
                                "description": "Name for this component (e.g. 'api', 'frontend', 'shared'). Used as the subdirectory and within generated files."
                            },
                            "template_id": {
                                "type": "string",
                                "description": "Exact template ID (e.g. 'node/express-api', 'dotnet/webapi'). Takes precedence over language/kind."
                            },
                            "language": {
                                "type": "string",
                                "description": "Language name used to auto-select a template when template_id is omitted."
                            },
                            "kind": {
                                "type": "string",
                                "description": "Optional kind hint within a language group (e.g. 'cli', 'webapi', 'library')."
                            },
                            "parameters": {
                                "type": "object",
                                "description": "Optional template-specific parameters as a key-value map."
                            }
                        },
                        "required": ["project_name"]
                    }
                },
                "run_id": {
                    "type": "string",
                    "description": "Run/session ID that scopes all artifacts. Required."
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
            "required": ["root_name", "components", "run_id"]
        }
        """).RootElement;
}
