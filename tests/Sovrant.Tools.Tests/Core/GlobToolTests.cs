using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

/// <summary>Tests for <see cref="GlobTool"/>.</summary>
public sealed class GlobToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-glob-{Guid.NewGuid():N}");

    public GlobToolTests()
    {
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "src"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "docs"));
        File.WriteAllText(Path.Combine(_tempDir, "src", "app.cs"), "class App {}");
        File.WriteAllText(Path.Combine(_tempDir, "src", "helper.cs"), "class Helper {}");
        File.WriteAllText(Path.Combine(_tempDir, "src", "readme.md"), "# Readme");
        File.WriteAllText(Path.Combine(_tempDir, "docs", "guide.md"), "# Guide");
        File.WriteAllText(Path.Combine(_tempDir, "root.txt"), "root file");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    [Fact]
    public async Task Glob_MatchesCsFiles()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*.cs", path = _tempDir }));

        Assert.Contains("app.cs", result);
        Assert.Contains("helper.cs", result);
        Assert.DoesNotContain("readme.md", result);
        Assert.DoesNotContain("guide.md", result);
    }

    [Fact]
    public async Task Glob_MatchesMdFiles()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*.md", path = _tempDir }));

        Assert.Contains("readme.md", result);
        Assert.Contains("guide.md", result);
        Assert.DoesNotContain("app.cs", result);
    }

    [Fact]
    public async Task Glob_SubdirectoryRestriction()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*.md", path = Path.Combine(_tempDir, "docs") }));

        Assert.Contains("guide.md", result);
        Assert.DoesNotContain("readme.md", result);
    }

    [Fact]
    public async Task Glob_NoMatches_ReturnsMessage()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*.xyz", path = _tempDir }));

        Assert.Equal("No files found.", result);
    }

    [Fact]
    public async Task Glob_MissingPattern_ReturnsError()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Glob_NonexistentDirectory_ReturnsError()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*", path = "/nonexistent/dir/xyz" }));

        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Glob_WildcardAll_MatchesAllFiles()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "**/*", path = _tempDir }));

        Assert.Contains("app.cs", result);
        Assert.Contains("guide.md", result);
        Assert.Contains("root.txt", result);
    }

    [Fact]
    public async Task Glob_SingleDirectoryPattern()
    {
        var tool = new GlobTool();
        var result = await tool.ExecuteAsync(MakeInput(new { pattern = "*.txt", path = _tempDir }));

        Assert.Contains("root.txt", result);
        Assert.DoesNotContain("app.cs", result);
    }
}
