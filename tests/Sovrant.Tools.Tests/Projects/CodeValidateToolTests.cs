using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Projects.Templates;
using Sovrant.Tools.Projects;
using Sovrant.Tools.Projects.Scaffolds.DotNet;
using Sovrant.Tools.Projects.Scaffolds.Go;
using Sovrant.Tools.Projects.Scaffolds.Java;
using Sovrant.Tools.Projects.Scaffolds.Minimal;
using Sovrant.Tools.Projects.Scaffolds.Node;
using Sovrant.Tools.Projects.Scaffolds.Python;
using Sovrant.Tools.Projects.Scaffolds.Rust;

namespace Sovrant.Tools.Tests.Projects;

/// <summary>
/// Phase 128E — Unit and integration tests for <see cref="CodeValidateTool"/>.
/// Uses a real <see cref="LocalArtifactStore"/> in a temp directory.
/// No compiler or toolchain required — purely structural file checks.
/// </summary>
public sealed class CodeValidateToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalArtifactStore _store;
    private readonly CodeValidateTool _tool;

    public CodeValidateToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant_validate_{Guid.NewGuid():N}");
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _tempDir);
        _tool  = new CodeValidateTool(_store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task WriteFileAsync(ArtifactHandle handle, string path, string content = "x")
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await _store.WriteAsync(handle, path, ms);
    }

    private static JsonElement BuildInput(string runId, string workspaceId = "test-ws") =>
        JsonDocument.Parse($$"""{"run_id":"{{runId}}","workspace_id":"{{workspaceId}}"}""").RootElement;

    private static ArtifactScope Scope(string runId) =>
        new() { WorkspaceId = "test-ws", RunId = runId };

    // ── Error cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task MissingRunId_ReturnsError()
    {
        var json = await _tool.ExecuteAsync(
            JsonDocument.Parse("""{"workspace_id":"test-ws"}""").RootElement,
            CancellationToken.None);

        Assert.StartsWith("Error:", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyRun_ReturnsError()
    {
        // CreateRunScopeAsync creates the dir + blank manifest, but no files.
        var json = await _tool.ExecuteAsync(BuildInput("empty-run-001"), CancellationToken.None);
        Assert.StartsWith("Error:", json, StringComparison.Ordinal);
    }

    // ── No code manifest — universal gates only ───────────────────────────

    [Fact]
    public async Task NoCodeManifest_AllUniversalFilesPresent_Passes()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-nometa-pass"));
        await WriteFileAsync(handle, "proj/README.md");
        await WriteFileAsync(handle, "proj/.gitignore");
        await WriteFileAsync(handle, "proj/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-nometa-pass"), CancellationToken.None))
            .RootElement;

        Assert.True(result.GetProperty("pass").GetBoolean());
        Assert.Equal("unknown", result.GetProperty("language").GetString());
        Assert.Equal(3, result.GetProperty("gates_total").GetInt32()); // 3 universal gates only
        Assert.Equal(0, result.GetProperty("gates_failed").GetInt32());
    }

    [Fact]
    public async Task NoCodeManifest_MissingUniversalFiles_Fails()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-nometa-fail"));
        await WriteFileAsync(handle, "proj/src/main.rs"); // has a file, so no empty-run error

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-nometa-fail"), CancellationToken.None))
            .RootElement;

        Assert.False(result.GetProperty("pass").GetBoolean());
        Assert.True(result.GetProperty("gates_failed").GetInt32() > 0);
        Assert.NotEmpty(result.GetProperty("remediation").EnumerateArray());
    }

    // ── .NET gates ────────────────────────────────────────────────────────

    [Fact]
    public async Task DotNet_FullScaffold_PassesAllGates()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-dotnet-pass"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "dotnet", Kind = "webapi" });

        await WriteFileAsync(handle, "MyApp/MyApp.sln");
        await WriteFileAsync(handle, "MyApp/Directory.Build.props");
        await WriteFileAsync(handle, "MyApp/README.md");
        await WriteFileAsync(handle, "MyApp/.gitignore");
        await WriteFileAsync(handle, "MyApp/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-dotnet-pass"), CancellationToken.None))
            .RootElement;

        Assert.True(result.GetProperty("pass").GetBoolean(), GetFailureMessage(result));
        Assert.Equal("dotnet", result.GetProperty("language").GetString());
        Assert.Equal(0, result.GetProperty("gates_failed").GetInt32());
    }

    [Fact]
    public async Task DotNet_MissingSln_FailsCriticalGate()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-dotnet-nosln"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "dotnet" });

        await WriteFileAsync(handle, "MyApp/Directory.Build.props");
        await WriteFileAsync(handle, "MyApp/README.md");
        await WriteFileAsync(handle, "MyApp/.gitignore");
        await WriteFileAsync(handle, "MyApp/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-dotnet-nosln"), CancellationToken.None))
            .RootElement;

        Assert.False(result.GetProperty("pass").GetBoolean());
        var failedNames = result.GetProperty("gates").EnumerateArray()
            .Where(g => !g.GetProperty("passed").GetBoolean())
            .Select(g => g.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("dotnet-sln", failedNames);
    }

    [Fact]
    public async Task DotNet_SlnxFileAlsoPassesSlnGate()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-dotnet-slnx"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "dotnet" });

        await WriteFileAsync(handle, "MyApp/MyApp.slnx"); // .slnx instead of .sln
        await WriteFileAsync(handle, "MyApp/README.md");
        await WriteFileAsync(handle, "MyApp/.gitignore");
        await WriteFileAsync(handle, "MyApp/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-dotnet-slnx"), CancellationToken.None))
            .RootElement;

        var slnGate = result.GetProperty("gates").EnumerateArray()
            .First(g => g.GetProperty("name").GetString() == "dotnet-sln");
        Assert.True(slnGate.GetProperty("passed").GetBoolean());
    }

    // ── Node gate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Node_WithPackageJson_PassesCriticalGate()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-node-pass"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "node", Kind = "express-api" });

        await WriteFileAsync(handle, "api/package.json");
        await WriteFileAsync(handle, "api/README.md");
        await WriteFileAsync(handle, "api/.gitignore");
        await WriteFileAsync(handle, "api/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-node-pass"), CancellationToken.None))
            .RootElement;

        Assert.True(result.GetProperty("pass").GetBoolean(), GetFailureMessage(result));
    }

    [Fact]
    public async Task Node_MissingPackageJson_FailsCriticalGate()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-node-nopkg"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "node" });
        await WriteFileAsync(handle, "api/src/index.ts");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-node-nopkg"), CancellationToken.None))
            .RootElement;

        Assert.False(result.GetProperty("pass").GetBoolean());
        var failedNames = result.GetProperty("gates").EnumerateArray()
            .Where(g => !g.GetProperty("passed").GetBoolean())
            .Select(g => g.GetProperty("name").GetString())
            .ToList();
        Assert.Contains("node-package-json", failedNames);
    }

    // ── Remediation ───────────────────────────────────────────────────────

    [Fact]
    public async Task FailedCriticalGate_HasCriticalRemediationPrefix()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-remediation"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest { Language = "node" });
        await WriteFileAsync(handle, "api/src/index.ts"); // no package.json

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-remediation"), CancellationToken.None))
            .RootElement;

        var remediation = result.GetProperty("remediation").EnumerateArray()
            .Select(r => r.GetString())
            .ToList();

        Assert.True(remediation.Any(r => r!.StartsWith("[CRITICAL]", StringComparison.Ordinal)),
            "Expected at least one CRITICAL remediation step");
    }

    // ── Response shape ────────────────────────────────────────────────────

    [Fact]
    public async Task Response_IncludesCommandsFromManifest()
    {
        var handle = await _store.CreateRunScopeAsync(Scope("run-commands"));
        await _store.SetCodeMetadataAsync(handle, new CodeManifest
        {
            Language = "go",
            Kind = "api",
            BuildCommand = "go build ./...",
            RunCommand = "go run main.go",
            TestCommand = "go test ./...",
        });
        await WriteFileAsync(handle, "myapi/go.mod");
        await WriteFileAsync(handle, "myapi/README.md");
        await WriteFileAsync(handle, "myapi/.gitignore");
        await WriteFileAsync(handle, "myapi/.github/workflows/ci.yml");

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput("run-commands"), CancellationToken.None))
            .RootElement;

        Assert.Equal("go build ./...", result.GetProperty("build_command").GetString());
        Assert.Equal("go run main.go", result.GetProperty("run_command").GetString());
        Assert.Equal("go test ./...", result.GetProperty("test_command").GetString());
    }

    // ── All 21 scaffold templates should pass ────────────────────────────

    public static IEnumerable<object[]> AllTemplateData =>
    [
        [new NodeExpressApiScaffold()],
        [new NodeCliScaffold()],
        [new NodeLibraryScaffold()],
        [new NodeNextJsScaffold()],
        [new NodeMonorepoScaffold()],
        [new DotNetWebApiScaffold()],
        [new DotNetConsoleScaffold()],
        [new DotNetLibraryScaffold()],
        [new DotNetWorkerScaffold()],
        [new DotNetBlazorScaffold()],
        [new PythonFastApiScaffold()],
        [new PythonScriptScaffold()],
        [new GoApiScaffold()],
        [new RustCliScaffold()],
        [new JavaMavenAppScaffold()],
        [new KotlinConsoleScaffold()],
        [new RubyScriptScaffold()],
        [new SwiftCliScaffold()],
        [new LuaScriptScaffold()],
        [new ZigCliScaffold()],
        [new CppCmakeScaffold()],
    ];

    [Theory]
    [MemberData(nameof(AllTemplateData))]
    public async Task AllScaffolds_PassCodeValidation(IProjectTemplate template)
    {
        const string projectName = "test-project";
        var runId = $"val-{template.Id.Replace('/', '-')}";

        var handle = await _store.CreateRunScopeAsync(Scope(runId));

        // Write all scaffold files under the project prefix (same layout as CodeCreateTool)
        foreach (var file in template.Scaffold(new ScaffoldContext(projectName)))
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(file.Content));
            await _store.WriteAsync(handle, $"{projectName}/{file.RelativePath}", ms);
        }

        await _store.SetCodeMetadataAsync(handle, new CodeManifest
        {
            Language   = template.Language,
            Kind       = template.Kind,
            TemplateId = template.Id,
        });

        var result = JsonDocument.Parse(
            await _tool.ExecuteAsync(BuildInput(runId), CancellationToken.None))
            .RootElement;

        Assert.True(result.GetProperty("pass").GetBoolean(),
            $"[{template.Id}] validation failed. {GetFailureMessage(result)}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string GetFailureMessage(JsonElement result)
    {
        var failed = result.GetProperty("gates").EnumerateArray()
            .Where(g => !g.GetProperty("passed").GetBoolean())
            .Select(g => $"{g.GetProperty("name").GetString()} [{g.GetProperty("severity").GetString()}]: {g.GetProperty("check").GetString()}")
            .ToList();
        return failed.Count > 0
            ? $"Failed gates: {string.Join("; ", failed)}"
            : string.Empty;
    }
}
