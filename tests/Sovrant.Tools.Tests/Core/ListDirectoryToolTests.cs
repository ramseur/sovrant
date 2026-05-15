using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

/// <summary>Tests for <see cref="ListDirectoryTool"/>.</summary>
public sealed class ListDirectoryToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-ls-{Guid.NewGuid():N}");

    public ListDirectoryToolTests()
    {
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));
        File.WriteAllText(Path.Combine(_tempDir, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(_tempDir, "file2.cs"), "class X {}");
        File.WriteAllText(Path.Combine(_tempDir, "subdir", "nested.md"), "# Nested");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    [Fact]
    public async Task LS_ListsFilesAndDirectories()
    {
        var tool = new ListDirectoryTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.Contains("file1.txt", result);
        Assert.Contains("file2.cs", result);
        Assert.Contains("subdir/", result);
    }

    [Fact]
    public async Task LS_DirectoriesHaveTrailingSlash()
    {
        var tool = new ListDirectoryTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = _tempDir }));

        Assert.Contains("subdir/", result);
    }

    [Fact]
    public async Task LS_NonexistentPath_ReturnsError()
    {
        var tool = new ListDirectoryTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = "/nonexistent/dir/xyz" }));

        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LS_FileInsteadOfDirectory_ReturnsError()
    {
        var tool = new ListDirectoryTool();
        var filePath = Path.Combine(_tempDir, "file1.txt");
        var result = await tool.ExecuteAsync(MakeInput(new { path = filePath }));

        Assert.Contains("is a file", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LS_EmptyDirectory_ReturnsJustPath()
    {
        var emptyDir = Path.Combine(_tempDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var tool = new ListDirectoryTool();
        var result = await tool.ExecuteAsync(MakeInput(new { path = emptyDir }));

        Assert.Contains(emptyDir, result);
        // Only the directory path line, no entries
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }
}
