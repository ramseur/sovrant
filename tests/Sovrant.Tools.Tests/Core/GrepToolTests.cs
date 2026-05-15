using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

/// <summary>Tests for <see cref="GrepTool"/>.</summary>
public sealed class GrepToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-grep-{Guid.NewGuid():N}");

    public GrepToolTests()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "alpha.cs"), "public class Alpha\n{\n    int Count = 42;\n}\n");
        File.WriteAllText(Path.Combine(_tempDir, "beta.cs"), "public class Beta\n{\n    string Name = \"hello\";\n}\n");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "Alpha is the first.\nBeta is the second.\nGamma is third.\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    // ── files_with_matches mode ──────────────────────────────────────────────

    [Fact]
    public async Task Grep_FilesWithMatches_ReturnsMatchingFiles()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "class",
            path = _tempDir,
            output_mode = "files_with_matches",
        }));

        Assert.Contains("alpha.cs", result);
        Assert.Contains("beta.cs", result);
        Assert.DoesNotContain("notes.txt", result);
    }

    // ── content mode ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Grep_ContentMode_ReturnsMatchingLines()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "Alpha",
            path = _tempDir,
            output_mode = "content",
        }));

        Assert.Contains("class Alpha", result);
        Assert.Contains("Alpha is the first", result);
    }

    [Fact]
    public async Task Grep_ContentMode_ShowsLineNumbers()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "Count",
            path = _tempDir,
            output_mode = "content",
        }));

        // Line number should be present (Count is on line 3 of alpha.cs)
        Assert.Contains(":3:", result);
    }

    // ── count mode ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Grep_CountMode_ReturnsMatchCounts()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "is",
            path = Path.Combine(_tempDir, "notes.txt"),
            output_mode = "count",
        }));

        // "is" appears in all 3 lines of notes.txt
        Assert.Contains("notes.txt", result);
    }

    // ── case insensitive ─────────────────────────────────────────────────────

    [Fact]
    public async Task Grep_CaseInsensitive_MatchesBothCases()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "ALPHA",
            path = _tempDir,
            output_mode = "files_with_matches",
            case_insensitive = true,
        }));

        Assert.Contains("alpha.cs", result);
        Assert.Contains("notes.txt", result);
    }

    [Fact]
    public async Task Grep_CaseSensitive_DoesNotMatchWrongCase()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "ALPHA",
            path = _tempDir,
            output_mode = "files_with_matches",
        }));

        Assert.Equal("No matches found.", result);
    }

    // ── glob filter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Grep_GlobFilter_RestrictsToFileType()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "Alpha",
            path = _tempDir,
            glob = "*.cs",
            output_mode = "files_with_matches",
        }));

        Assert.Contains("alpha.cs", result);
        Assert.DoesNotContain("notes.txt", result);
    }

    // ── single file search ───────────────────────────────────────────────────

    [Fact]
    public async Task Grep_SingleFile_SearchesOneFile()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "Beta",
            path = Path.Combine(_tempDir, "notes.txt"),
            output_mode = "content",
        }));

        Assert.Contains("Beta is the second", result);
    }

    // ── error cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Grep_MissingPattern_ReturnsError()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Grep_InvalidRegex_ReturnsError()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "[invalid",
            path = _tempDir,
        }));

        Assert.Contains("invalid regex", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Grep_NonexistentPath_ReturnsError()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "test",
            path = "/nonexistent/path/xyz",
        }));

        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Grep_NoMatches_ReturnsMessage()
    {
        var tool = new GrepTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            pattern = "zzzzz_no_match",
            path = _tempDir,
        }));

        Assert.Equal("No matches found.", result);
    }
}
