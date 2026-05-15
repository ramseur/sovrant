using System.Text.Json;
using Sovrant.Lsp.Types;
using Sovrant.Tools.Lsp;

namespace Sovrant.Lsp.Tests;

/// <summary>Tests for LSP tool wrappers using a fake LSP client.</summary>
public sealed class LspToolTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task LspHover_ReturnsError_WhenNoClient()
    {
        var tool = new LspHoverTool(new EmptyClientManager());
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("no language server configured", result);
    }

    [Fact]
    public async Task LspHover_ReturnsError_WhenNotRunning()
    {
        var tool = new LspHoverTool(new FakeClientManager(running: false));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("is not running", result);
    }

    [Fact]
    public async Task LspHover_ReturnsContent_WhenRunning()
    {
        var tool = new LspHoverTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Equal("string FakeType", result);
    }

    [Fact]
    public async Task LspDefinition_ReturnsError_WhenNoClient()
    {
        var tool = new LspDefinitionTool(new EmptyClientManager());
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("no language server configured", result);
    }

    [Fact]
    public async Task LspDefinition_ReturnsLocation_WhenRunning()
    {
        var tool = new LspDefinitionTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("FakeFile.cs:10:5", result);
    }

    [Fact]
    public async Task LspReferences_ReturnsLocations_WhenRunning()
    {
        var tool = new LspReferencesTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("2 reference(s)", result);
    }

    [Fact]
    public async Task LspDiagnostics_ReturnsDiagnostics_WhenRunning()
    {
        var tool = new LspDiagnosticsTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs"}"""));
        Assert.Contains("1 diagnostic(s)", result);
        Assert.Contains("error", result);
        Assert.Contains("Null reference", result);
    }

    [Fact]
    public async Task LspRename_ReturnsError_WhenNotRunning()
    {
        var tool = new LspRenameTool(new FakeClientManager(running: false));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0,"new_name":"Foo"}"""));
        Assert.Contains("is not running", result);
    }

    [Fact]
    public async Task LspRename_ReturnsPreview_WhenRunning()
    {
        var tool = new LspRenameTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0,"new_name":"NewName"}"""));
        Assert.Contains("Rename preview", result);
        Assert.Contains("NewName", result);
    }

    [Fact]
    public async Task LspHover_RequiresFilePath()
    {
        var tool = new LspHoverTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"line":0,"character":0}"""));
        Assert.Contains("file_path is required", result);
    }

    [Fact]
    public async Task LspRename_RequiresNewName()
    {
        var tool = new LspRenameTool(new FakeClientManager(running: true));
        var result = await tool.ExecuteAsync(Json("""{"file_path":"test.cs","line":0,"character":0}"""));
        Assert.Contains("new_name is required", result);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class EmptyClientManager : ILspClientManager
    {
        public IReadOnlyList<string> Languages => [];
        public ILspClient? GetClient(string language) => null;
        public ILspClient? GetClientForFile(string filePath) => null;
        public Task StartAllAsync(string workspaceRoot, CancellationToken ct) => Task.CompletedTask;
        public Task StopAllAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeClientManager : ILspClientManager
    {
        private readonly FakeLspClient _client;
        public FakeClientManager(bool running) => _client = new FakeLspClient(running);
        public IReadOnlyList<string> Languages => ["csharp"];
        public ILspClient? GetClient(string language) =>
            string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase) ? _client : null;
        public ILspClient? GetClientForFile(string filePath) =>
            filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? _client : null;
        public Task StartAllAsync(string workspaceRoot, CancellationToken ct) => Task.CompletedTask;
        public Task StopAllAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeLspClient : ILspClient
    {
        public string Language => "csharp";
        public bool IsRunning { get; }

        public FakeLspClient(bool running) => IsRunning = running;

        public Task StartAsync(string workspaceRoot, CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<HoverResult?> HoverAsync(string filePath, int line, int character, CancellationToken ct) =>
            Task.FromResult<HoverResult?>(new HoverResult
            {
                Contents = new MarkupContent { Kind = "plaintext", Value = "string FakeType" },
            });

        public Task<IReadOnlyList<Location>> DefinitionAsync(string filePath, int line, int character, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Location>>([
                new Location
                {
                    Uri = LspClient.PathToUri("FakeFile.cs"),
                    Range = new Types.Range
                    {
                        Start = new Position { Line = 9, Character = 4 },
                        End = new Position { Line = 9, Character = 20 },
                    },
                },
            ]);

        public Task<IReadOnlyList<Location>> ReferencesAsync(string filePath, int line, int character, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Location>>([
                new Location { Uri = "file:///Ref1.cs", Range = new() },
                new Location { Uri = "file:///Ref2.cs", Range = new() },
            ]);

        public IReadOnlyList<Diagnostic> GetDiagnostics(string filePath) => [
            new Diagnostic
            {
                Range = new Types.Range
                {
                    Start = new Position { Line = 5, Character = 0 },
                    End = new Position { Line = 5, Character = 10 },
                },
                Severity = DiagnosticSeverity.Error,
                Code = "CS0001",
                Message = "Null reference",
            },
        ];

        public Task<WorkspaceEdit?> RenameAsync(string filePath, int line, int character, string newName, CancellationToken ct) =>
            Task.FromResult<WorkspaceEdit?>(new WorkspaceEdit
            {
                Changes = new Dictionary<string, TextEdit[]>
                {
                    ["file:///test.cs"] = [new TextEdit
                    {
                        Range = new Types.Range
                        {
                            Start = new Position { Line = 0, Character = 0 },
                            End = new Position { Line = 0, Character = 10 },
                        },
                        NewText = newName,
                    }],
                },
            });

        public Task NotifyDidOpenAsync(string filePath, string text, CancellationToken ct) => Task.CompletedTask;
        public Task NotifyDidChangeAsync(string filePath, string text, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
