using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Tools.Projects;

/// <summary>
/// Phase 128E — Validates the structural quality of a previously scaffolded code artifact run.
/// Reads the run manifest (language, kind, build commands) then checks that language-specific
/// marker files and universal scaffold files are present. Purely structural — no compiler required.
/// </summary>
public sealed class CodeValidateTool : ITool
{
    private static readonly ToolDefinition s_definition = new("CodeValidate", CreateSchema())
    {
        Description =
            "Validate the structural quality of a previously scaffolded code artifact run. " +
            "Reads the run manifest (language, kind, build commands) and verifies that " +
            "language-specific marker files (.sln for .NET, package.json for Node, go.mod for Go, etc.) " +
            "and universal files (.gitignore, README.md, .github/workflows/ci.yml) are present. " +
            "Returns pass/fail per gate with severity (critical/warning) and remediation steps. " +
            "Does not require a compiler in PATH — purely structural checks via the artifact store. " +
            "Run this immediately after CodeCreate to confirm the scaffold is complete.",
    };

    private readonly IArtifactStore _store;

    public CodeValidateTool(IArtifactStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ToolDefinition Definition => s_definition;

    // ── Gate definitions ─────────────────────────────────────────────────

    private sealed record GateDef(
        string Name,
        string Check,
        string Severity,
        Func<IReadOnlySet<string>, bool> Test);

    private static readonly GateDef[] s_universalGates =
    [
        new("readme",      "README.md present",                "warning", HasFile("README.md")),
        new("gitignore",   ".gitignore present",               "warning", HasFile(".gitignore")),
        new("ci-workflow", ".github/workflows/ci.yml present", "warning", HasPathSegment(".github/workflows/ci")),
    ];

    private static readonly Dictionary<string, GateDef[]> s_languageGates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dotnet"] =
            [
                new("dotnet-sln",         ".sln or .slnx file present (required for dotnet build)",  "critical", AnyOf(HasExtension(".sln"), HasExtension(".slnx"))),
                new("dotnet-build-props", "Directory.Build.props present (solution-wide settings)",  "warning",  HasFile("Directory.Build.props")),
            ],
            ["node"] =
            [
                new("node-package-json",  "package.json present (required for npm install)",         "critical", HasFile("package.json")),
            ],
            ["python"] =
            [
                new("python-project",     "pyproject.toml or setup.py present",                      "critical", AnyOf(HasFile("pyproject.toml"), HasFile("setup.py"))),
            ],
            ["go"] =
            [
                new("go-mod",             "go.mod present (required for go build)",                  "critical", HasFile("go.mod")),
            ],
            ["rust"] =
            [
                new("rust-cargo",         "Cargo.toml present (required for cargo build)",           "critical", HasFile("Cargo.toml")),
            ],
            ["java"] =
            [
                new("java-pom",           "pom.xml present (required for mvn package)",              "critical", HasFile("pom.xml")),
            ],
            ["kotlin"] =
            [
                new("kotlin-gradle",      "build.gradle.kts present (required for gradle build)",    "critical", HasFile("build.gradle.kts")),
            ],
            ["ruby"] =
            [
                new("ruby-gemfile",       "Gemfile present (required for bundle install)",           "critical", HasFile("Gemfile")),
            ],
            ["swift"] =
            [
                new("swift-package",      "Package.swift present (required for swift build)",        "critical", HasFile("Package.swift")),
            ],
            ["lua"] =
            [
                new("lua-rockspec",       "*.rockspec file present",                                  "critical", HasExtension(".rockspec")),
            ],
            ["zig"] =
            [
                new("zig-build",          "build.zig present (required for zig build)",              "critical", HasFile("build.zig")),
            ],
            ["cpp"] =
            [
                new("cpp-cmake",          "CMakeLists.txt present (required for cmake -B build)",    "critical", HasFile("CMakeLists.txt")),
            ],
        };

    // ── Gate predicate helpers ────────────────────────────────────────────

    private static Func<IReadOnlySet<string>, bool> HasFile(string fileName) =>
        files => files.Any(p =>
        {
            var slash = p.LastIndexOf('/');
            return p.AsSpan(slash + 1).Equals(fileName.AsSpan(), StringComparison.OrdinalIgnoreCase);
        });

    private static Func<IReadOnlySet<string>, bool> HasExtension(string ext) =>
        files => files.Any(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static Func<IReadOnlySet<string>, bool> HasPathSegment(string segment) =>
        files => files.Any(p => p.Contains(segment, StringComparison.OrdinalIgnoreCase));

    private static Func<IReadOnlySet<string>, bool> AnyOf(
        Func<IReadOnlySet<string>, bool> a,
        Func<IReadOnlySet<string>, bool> b) =>
        files => a(files) || b(files);

    // ── Execution ─────────────────────────────────────────────────────────

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var runId = input.GetStringProp("run_id");
        if (string.IsNullOrWhiteSpace(runId))
            return "Error: 'run_id' is required.";

        var scope = new ArtifactScope
        {
            WorkspaceId = input.GetStringProp("workspace_id", WorkspaceIdentity.DefaultPersonal()),
            ProjectId   = input.GetStringProp("project_id",   ArtifactScope.DefaultProjectId),
            RunId       = runId,
        };

        ArtifactHandle handle;
        try
        {
            handle = await _store.CreateRunScopeAsync(scope, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        // Read manifest to discover language, kind, and commands.
        CodeManifest? code = null;
        try
        {
            using var manifestStream = await _store.ReadAsync(handle, "_manifest.json", ct).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<ArtifactManifest>(
                manifestStream, cancellationToken: ct).ConfigureAwait(false);
            code = manifest?.Code;
        }
        catch (FileNotFoundException) { /* no manifest — universal gates only */ }
        catch (JsonException)         { /* corrupt manifest — universal gates only */ }

        // Collect all file paths for the run.
        var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var entry in _store.ListAsync(scope, ct).ConfigureAwait(false))
            filePaths.Add(entry.RelativePath);

        if (filePaths.Count == 0 && code is null)
            return $"Error: run '{runId}' has no files. " +
                   "Verify the run_id, workspace_id, and project_id are correct.";

        // Select and order gates: language-specific first (critical), then universal.
        var language = code?.Language ?? "unknown";
        var activeGates = new List<GateDef>();
        if (s_languageGates.TryGetValue(language, out var langGates))
            activeGates.AddRange(langGates);
        activeGates.AddRange(s_universalGates);

        // Evaluate all gates.
        var results = activeGates.Select(g => new
        {
            name     = g.Name,
            check    = g.Check,
            severity = g.Severity,
            passed   = g.Test(filePaths),
        }).ToList();

        var failed = results.Where(r => !r.passed).ToList();
        var pass   = failed.Count == 0;

        var remediation = failed
            .Select(r => r.severity == "critical"
                ? $"[CRITICAL] {r.check} — add this file before attempting to build"
                : $"[WARNING]  {r.check} — add this file to match the scaffold standard")
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            pass,
            language,
            kind         = code?.Kind,
            template_id  = code?.TemplateId,
            build_command  = code?.BuildCommand,
            run_command    = code?.RunCommand,
            test_command   = code?.TestCommand,
            file_count   = filePaths.Count,
            gates_total  = results.Count,
            gates_passed = results.Count(r => r.passed),
            gates_failed = failed.Count,
            gates        = results,
            remediation,
        });
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "run_id": {
                    "type": "string",
                    "description": "The run ID of the scaffolded artifact to validate. Required."
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
            "required": ["run_id"]
        }
        """).RootElement;
}
