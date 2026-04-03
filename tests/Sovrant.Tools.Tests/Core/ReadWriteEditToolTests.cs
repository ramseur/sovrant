using System.Text.Json;
using Sovrant.Tools.Core;

namespace Sovrant.Tools.Tests.Core;

public sealed class ReadWriteEditToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-tests-{Guid.NewGuid():N}");

    public ReadWriteEditToolTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // ── WriteTool ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Write_CreatesFile()
    {
        var tool = new WriteFileTool();
        var path = TempFile("hello.txt");
        var input = MakeInput(new { file_path = path, content = "Hello, World!" });

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("written", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Hello, World!", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Write_OverwritesExistingFile()
    {
        var tool = new WriteFileTool();
        var path = TempFile("overwrite.txt");
        await File.WriteAllTextAsync(path, "old content");
        var input = MakeInput(new { file_path = path, content = "new content" });

        await tool.ExecuteAsync(input);

        Assert.Equal("new content", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Write_MissingFilePath_ReturnsError()
    {
        var tool = new WriteFileTool();
        var result = await tool.ExecuteAsync(MakeInput(new { content = "x" }));
        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── ReadTool ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_ReturnsLinesWithNumbers()
    {
        var tool = new ReadFileTool();
        var path = TempFile("read.txt");
        await File.WriteAllTextAsync(path, "line1\nline2\nline3");
        var input = MakeInput(new { file_path = path });

        var result = await tool.ExecuteAsync(input);

        Assert.Contains("line1", result, StringComparison.Ordinal);
        Assert.Contains("line2", result, StringComparison.Ordinal);
        Assert.Contains("line3", result, StringComparison.Ordinal);
        Assert.Contains("→", result, StringComparison.Ordinal); // line-number arrow
    }

    [Fact]
    public async Task Read_OffsetAndLimit()
    {
        var tool = new ReadFileTool();
        var path = TempFile("paged.txt");
        await File.WriteAllTextAsync(path, "a\nb\nc\nd\ne");
        var input = MakeInput(new { file_path = path, offset = 2, limit = 2 });

        var result = await tool.ExecuteAsync(input);

        // Lines c and d (offset=2, limit=2) should be present
        Assert.Contains("\u2192c", result, StringComparison.Ordinal);
        Assert.Contains("\u2192d", result, StringComparison.Ordinal);
        // Line a (before offset) and e (after limit) should not appear as numbered lines
        Assert.DoesNotContain("\u2192a", result, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2192e", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_MissingFile_ReturnsError()
    {
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(MakeInput(new { file_path = "/nonexistent/path.txt" }));
        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── EditTool ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_ReplaceFirst_Succeeds()
    {
        var tool = new EditFileTool();
        var path = TempFile("edit.txt");
        await File.WriteAllTextAsync(path, "foo bar foo");
        var input = MakeInput(new { file_path = path, old_string = "foo", new_string = "baz", replace_all = false });

        var result = await tool.ExecuteAsync(input);

        Assert.DoesNotContain("Error", result, StringComparison.OrdinalIgnoreCase);
        var content = await File.ReadAllTextAsync(path);
        Assert.Equal("baz bar foo", content);
    }

    [Fact]
    public async Task Edit_ReplaceAll_Succeeds()
    {
        var tool = new EditFileTool();
        var path = TempFile("edit-all.txt");
        await File.WriteAllTextAsync(path, "foo bar foo");
        var input = MakeInput(new { file_path = path, old_string = "foo", new_string = "baz", replace_all = true });

        await tool.ExecuteAsync(input);

        Assert.Equal("baz bar baz", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Edit_StringNotFound_ReturnsError()
    {
        var tool = new EditFileTool();
        var path = TempFile("not-found.txt");
        await File.WriteAllTextAsync(path, "hello world");
        var input = MakeInput(new { file_path = path, old_string = "xyz", new_string = "abc" });

        var result = await tool.ExecuteAsync(input);

        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Edit_RoundTrip_WriteReadEdit()
    {
        var writeTool = new WriteFileTool();
        var readTool = new ReadFileTool();
        var editTool = new EditFileTool();
        var path = TempFile("roundtrip.txt");

        // Write
        await writeTool.ExecuteAsync(MakeInput(new { file_path = path, content = "Hello World\nLine Two" }));

        // Read
        var readResult = await readTool.ExecuteAsync(MakeInput(new { file_path = path }));
        Assert.Contains("Hello World", readResult, StringComparison.Ordinal);

        // Edit
        await editTool.ExecuteAsync(MakeInput(new { file_path = path, old_string = "Hello World", new_string = "Goodbye World" }));

        // Verify
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Goodbye World", content, StringComparison.Ordinal);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static JsonElement MakeInput(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }
}
