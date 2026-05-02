using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Tools.Artifacts;

namespace Sovrant.Tools.Tests.Artifacts;

/// <summary>Tests for <see cref="ArtifactTool"/>.</summary>
public sealed class ArtifactToolTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IArtifactStore _store;
    private readonly ArtifactTool _tool;

    public ArtifactToolTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sovrant-artifact-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);

        // LocalArtifactStore uses SOVRANT_ARTIFACTS_ROOT or ~/.sovrant/artifacts.
        // We override via the constructor overload that accepts a root path.
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _tempRoot);
        _tool = new ArtifactTool(_store);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
    }

    // ── Definition ────────────────────────────────────────────────────────────

    [Fact]
    public void Definition_Name_IsArtifact()
    {
        Assert.Equal("Artifact", _tool.Definition.Name);
    }

    [Fact]
    public void Definition_Description_DescribesFileSaving()
    {
        // Phase 87 Track B reframed Artifact as the default write target for
        // any generated file (was previously agent-only). The description
        // should make that purpose obvious.
        var desc = _tool.Definition.Description;
        Assert.Contains("file", desc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("write", desc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Definition_Schema_RequiresAction()
    {
        var schema = _tool.Definition.InputSchema;
        var required = schema.GetProperty("required");
        var names = required.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("action", names);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Write_ValidInput_ReturnsWrittenStatus()
    {
        var input = Parse("""
            {"action":"write","path":"report.md","content":"# Hello","run_id":"run-1"}
        """);
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("written", result, StringComparison.Ordinal);
        Assert.Contains("report.md", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Write_MissingPath_ReturnsError()
    {
        var input = Parse("""{"action":"write","content":"x","run_id":"run-1"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("path", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Write_MissingContent_ReturnsError()
    {
        var input = Parse("""{"action":"write","path":"f.txt","run_id":"run-1"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("content", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Write_MissingRunId_ReturnsError()
    {
        var input = Parse("""{"action":"write","path":"f.txt","content":"x"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("run_id", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── WriteMany ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteMany_MultipleEntries_AllPersisted()
    {
        var input = Parse("""
            {
                "action":"write_many",
                "run_id":"run-many-1",
                "entries":[
                    {"path":"a.txt","content":"alpha"},
                    {"path":"sub/b.txt","content":"beta","content_type":"text/plain"},
                    {"path":"c.json","content":"{\"k\":1}","content_type":"application/json"}
                ]
            }
        """);
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("\"status\":\"written\"", result, StringComparison.Ordinal);
        Assert.Contains("\"written_count\":3", result, StringComparison.Ordinal);

        // Each file readable back individually — parse the read response to
        // avoid grappling with how System.Text.Json escapes inner JSON content.
        foreach (var (path, expected) in new[] { ("a.txt", "alpha"), ("sub/b.txt", "beta"), ("c.json", "{\"k\":1}") })
        {
            var read = await _tool.ExecuteAsync(Parse(
                $$"""{"action":"read","path":"{{path}}","run_id":"run-many-1"}"""));
            using var doc = JsonDocument.Parse(read);
            Assert.Equal(expected, doc.RootElement.GetProperty("content").GetString());
        }
    }

    [Fact]
    public async Task WriteMany_MissingEntries_ReturnsError()
    {
        var input = Parse("""{"action":"write_many","run_id":"run-many-2"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("entries", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteMany_EmptyEntries_ReturnsError()
    {
        var input = Parse("""{"action":"write_many","run_id":"run-many-3","entries":[]}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteMany_MissingRunId_ReturnsError()
    {
        var input = Parse("""
            {"action":"write_many","entries":[{"path":"a.txt","content":"x"}]}
        """);
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("run_id", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteMany_PartialBadEntry_ReturnsPartialStatus()
    {
        var input = Parse("""
            {
                "action":"write_many",
                "run_id":"run-many-4",
                "entries":[
                    {"path":"good.txt","content":"ok"},
                    {"path":"bad.txt"}
                ]
            }
        """);
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("\"status\":\"partial\"", result, StringComparison.Ordinal);
        Assert.Contains("\"written_count\":1", result, StringComparison.Ordinal);
        Assert.Contains("\"error_count\":1", result, StringComparison.Ordinal);
    }

    // ── Read ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_AfterWrite_ReturnsContent()
    {
        // Write first
        var writeInput = Parse("""
            {"action":"write","path":"data.txt","content":"hello world","run_id":"run-2"}
        """);
        await _tool.ExecuteAsync(writeInput);

        // Read back
        var readInput = Parse("""{"action":"read","path":"data.txt","run_id":"run-2"}""");
        var result = await _tool.ExecuteAsync(readInput);
        Assert.Contains("hello world", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_NonExistent_ReturnsError()
    {
        var input = Parse("""{"action":"read","path":"nope.txt","run_id":"run-3"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Read_MissingRunId_ReturnsError()
    {
        var input = Parse("""{"action":"read","path":"f.txt"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("run_id", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_AfterWrite_ShowsArtifact()
    {
        var writeInput = Parse("""
            {"action":"write","path":"out.json","content":"{}","run_id":"run-4"}
        """);
        await _tool.ExecuteAsync(writeInput);

        var listInput = Parse("""{"action":"list","run_id":"run-4"}""");
        var result = await _tool.ExecuteAsync(listInput);
        Assert.Contains("out.json", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_EmptyScope_ReturnsZeroCount()
    {
        var input = Parse("""{"action":"list","run_id":"empty-run"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("\"count\":0", result, StringComparison.Ordinal);
    }

    // ── Unknown action ────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var input = Parse("""{"action":"delete"}""");
        var result = await _tool.ExecuteAsync(input);
        Assert.Contains("Unknown action", result, StringComparison.Ordinal);
    }

    // ── Cross-agent sharing scenario ──────────────────────────────────────────

    [Fact]
    public async Task Write_ThenRead_DifferentScope_SameRun_SharesArtifact()
    {
        // Agent A writes in workspace "ws1"
        var writeInput = Parse("""
            {"action":"write","path":"shared.md","content":"shared content","run_id":"team-run-1","workspace_id":"ws1","project_id":"proj1"}
        """);
        await _tool.ExecuteAsync(writeInput);

        // Agent B reads from same scope
        var readInput = Parse("""
            {"action":"read","path":"shared.md","run_id":"team-run-1","workspace_id":"ws1","project_id":"proj1"}
        """);
        var result = await _tool.ExecuteAsync(readInput);
        Assert.Contains("shared content", result, StringComparison.Ordinal);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;
}
