using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class SessionEndMemoryHandlerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileMemoryStore _memoryStore;
    private readonly InMemorySessionStore _sessionStore;
    private readonly SessionEndMemoryHandler _handler;

    public SessionEndMemoryHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _memoryStore = new FileMemoryStore(
            Path.Combine(_tempDir, "summaries"),
            Path.Combine(_tempDir, "learned"),
            Path.Combine(_tempDir, "instincts"),
            NullLogger<FileMemoryStore>.Instance);

        _sessionStore = new InMemorySessionStore();
        _handler = new SessionEndMemoryHandler(_sessionStore, _memoryStore, NullLogger<SessionEndMemoryHandler>.Instance);
    }

    public void Dispose()
    {
        _memoryStore.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task HandleSessionEnd_WithEnoughEntries_SavesSummary()
    {
        var entries = new[]
        {
            new SessionEntry("1", DateTimeOffset.UtcNow.AddMinutes(-5), "user", "Fix the bug"),
            new SessionEntry("2", DateTimeOffset.UtcNow.AddMinutes(-4), "assistant", "I'll fix it") { ToolName = "edit_file" },
            new SessionEntry("3", DateTimeOffset.UtcNow.AddMinutes(-3), "tool_result", "Done") { ToolName = "edit_file" },
            new SessionEntry("4", DateTimeOffset.UtcNow, "assistant", "Fixed!"),
        };

        foreach (var e in entries)
            await _sessionStore.AppendAsync("test-session", e);

        await _handler.HandleSessionEndAsync("test-session");

        var project = Directory.GetCurrentDirectory();
        var summaries = await _memoryStore.LoadSummariesAsync(project);
        Assert.Single(summaries);
        Assert.Equal("test-session", summaries[0].SessionId);
    }

    [Fact]
    public async Task HandleSessionEnd_TooFewEntries_SkipsSummary()
    {
        await _sessionStore.AppendAsync("short-session",
            new SessionEntry("1", DateTimeOffset.UtcNow, "user", "Hi"));

        await _handler.HandleSessionEndAsync("short-session");

        var project = Directory.GetCurrentDirectory();
        var summaries = await _memoryStore.LoadSummariesAsync(project);
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task HandleSessionEnd_NonexistentSession_DoesNotThrow()
    {
        await _handler.HandleSessionEndAsync("does-not-exist");
        // Should complete without throwing
    }

    /// <summary>Simple in-memory session store for testing.</summary>
    private sealed class InMemorySessionStore : ISessionStore
    {
        private readonly Dictionary<string, List<SessionEntry>> _sessions = new(StringComparer.Ordinal);

        public Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var list))
            {
                list = [];
                _sessions[sessionId] = list;
            }
            list.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SessionEntry>>(
                _sessions.TryGetValue(sessionId, out var list) ? list : []);
        }

        public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(_sessions.Keys.ToList());
        }
    }
}
