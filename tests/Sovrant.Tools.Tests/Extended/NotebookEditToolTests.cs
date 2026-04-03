using System.Text.Json;
using Sovrant.Tools.Extended;

namespace Sovrant.Tools.Tests.Extended;

public sealed class NotebookEditToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-nb-{Guid.NewGuid():N}");

    public NotebookEditToolTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    private const string SampleNotebook = """
        {
          "cells": [
            {"cell_type": "code", "source": "print('hello')", "metadata": {}, "outputs": [], "execution_count": null},
            {"cell_type": "markdown", "source": "# Title", "metadata": {}, "outputs": [], "execution_count": null}
          ],
          "metadata": {},
          "nbformat": 4,
          "nbformat_minor": 5
        }
        """;

    private static JsonElement MakeInput(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Read_ReturnsCellContents()
    {
        var path = TempFile("read.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        var result = await tool.ExecuteAsync(MakeInput(new { notebook_path = path }));

        Assert.Contains("print('hello')", result, StringComparison.Ordinal);
        Assert.Contains("# Title", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replace_UpdatesCellSource()
    {
        var path = TempFile("replace.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        var result = await tool.ExecuteAsync(MakeInput(new
        {
            notebook_path = path,
            edit_mode = "replace",
            cell_number = 0,
            new_source = "print('world')",
        }));

        Assert.DoesNotContain("Error", result, StringComparison.OrdinalIgnoreCase);
        var readResult = await tool.ExecuteAsync(MakeInput(new { notebook_path = path }));
        Assert.Contains("print('world')", readResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Insert_AddsCellAtIndex()
    {
        var path = TempFile("insert.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        await tool.ExecuteAsync(MakeInput(new
        {
            notebook_path = path,
            edit_mode = "insert",
            cell_number = 1,
            new_source = "x = 42",
            cell_type = "code",
        }));

        var readResult = await tool.ExecuteAsync(MakeInput(new { notebook_path = path }));
        Assert.Contains("x = 42", readResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Delete_RemovesCell()
    {
        var path = TempFile("delete.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        await tool.ExecuteAsync(MakeInput(new
        {
            notebook_path = path,
            edit_mode = "delete",
            cell_number = 0,
        }));

        var readResult = await tool.ExecuteAsync(MakeInput(new { notebook_path = path }));
        Assert.DoesNotContain("print('hello')", readResult, StringComparison.Ordinal);
        Assert.Contains("# Title", readResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replace_CaseInsensitiveEditMode()
    {
        var path = TempFile("ci.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        var result = await tool.ExecuteAsync(MakeInput(new
        {
            notebook_path = path,
            edit_mode = "REPLACE",
            cell_number = 0,
            new_source = "pass",
        }));

        Assert.DoesNotContain("Error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OutOfRange_ReturnsError()
    {
        var path = TempFile("oob.ipynb");
        await File.WriteAllTextAsync(path, SampleNotebook);
        var tool = new NotebookEditTool();

        var result = await tool.ExecuteAsync(MakeInput(new
        {
            notebook_path = path,
            edit_mode = "replace",
            cell_number = 99,
            new_source = "x",
        }));

        Assert.StartsWith("Error", result, StringComparison.OrdinalIgnoreCase);
    }
}
