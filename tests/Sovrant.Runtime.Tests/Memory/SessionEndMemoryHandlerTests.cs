using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class SessionEndMemoryHandlerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMemoryStore _memoryStore;
    private readonly InMemorySessionStore _sessionStore;
    private readonly SessionEndMemoryHandler _handler;

    public SessionEndMemoryHandlerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _memoryStore = new SqliteMemoryStore((ISqliteConnectionFactory)_provider);
        _sessionStore = new InMemorySessionStore();
        _handler = new SessionEndMemoryHandler(_sessionStore, _memoryStore, NullLogger<SessionEndMemoryHandler>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
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

        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var list))
            {
                list = [];
                _sessions[sessionId] = list;
            }
            list.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<SessionEntry>>(
                _sessions.TryGetValue(sessionId, out var list) ? list : []);
        }

        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(_sessions.Keys.ToList());
        }

        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult(_sessions.Remove(sessionId));

        public Task<int> DeleteAllAsync(CancellationToken ct = default)
        {
            var count = _sessions.Count;
            _sessions.Clear();
            return Task.FromResult(count);
        }

        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        private readonly Dictionary<string, string> _titles = new(StringComparer.Ordinal);

        public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
        {
            _titles[sessionId] = title;
            return Task.CompletedTask;
        }

        public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
        {
            _titles.TryGetValue(sessionId, out var title);
            return Task.FromResult<string?>(title);
        }

        public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
        {
            var result = _sessions.Keys
                .Select(id => new SessionListItem(id, _titles.GetValueOrDefault(id), DateTimeOffset.UtcNow))
                .ToList();
            return Task.FromResult<IReadOnlyList<SessionListItem>>(result);
        }
    }
}
