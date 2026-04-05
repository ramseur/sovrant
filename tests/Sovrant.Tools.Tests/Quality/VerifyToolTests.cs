using System.Text.Json;
using Sovrant.Tools.Quality;

namespace Sovrant.Tools.Tests.Quality;

public sealed class VerifyToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-verify-{Guid.NewGuid():N}");

    public VerifyToolTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    [Fact]
    public void Definition_HasCorrectName()
    {
        var tool = new VerifyTool();
        Assert.Equal("Verify", tool.Definition.Name);
    }

    [Fact]
    public async Task Execute_NonexistentPath_ReturnsError()
    {
        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = "/nonexistent/dir/xyz" }));
        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task Execute_UnknownProject_SkipsAllCommandPhases()
    {
        // Empty dir → Unknown project type → all command phases have no command → skip
        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.Contains("Unknown", result);
        // All command phases should be SKIP since no commands detected
        Assert.Contains("SKIP", result);
    }

    [Fact]
    public async Task Execute_SkipPhasesParam_SkipsSpecifiedPhases()
    {
        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            path = _tempDir,
            skip_phases = new[] { "Build", "TypeCheck", "Lint", "Test", "SecurityScan", "DiffReview" }
        }));

        // Everything skipped → all pass
        Assert.Contains("All gates passed", result);
    }

    [Fact]
    public async Task Execute_WithVerifyJson_RespectsSkipPhases()
    {
        var configDir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "verify.json"), """
        {
            "skip_phases": ["Build", "TypeCheck", "Lint", "Test", "SecurityScan", "DiffReview"]
        }
        """);

        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.Contains("All gates passed", result);
    }

    [Fact]
    public async Task Execute_DotnetProject_DetectsProjectType()
    {
        File.WriteAllText(Path.Combine(_tempDir, "Foo.csproj"), "<Project/>");

        // Skip all phases so we don't actually run dotnet commands
        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            path = _tempDir,
            skip_phases = new[] { "Build", "TypeCheck", "Lint", "Test", "SecurityScan", "DiffReview" }
        }));

        Assert.Contains("Dotnet", result);
    }

    [Fact]
    public async Task Execute_NodeProject_DetectsProjectType()
    {
        File.WriteAllText(Path.Combine(_tempDir, "package.json"), "{}");

        var tool = new VerifyTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            path = _tempDir,
            skip_phases = new[] { "Build", "TypeCheck", "Lint", "Test", "SecurityScan", "DiffReview" }
        }));

        Assert.Contains("Node", result);
    }
}
