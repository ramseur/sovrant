using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteSessionStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISessionStore _store;

    public SqliteSessionStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteSessionStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task AppendAndLoad_RoundTrips()
    {
        var entry = new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "Hello!");
        await _store.AppendAsync("s1", entry);

        var entries = await _store.LoadAsync("s1");

        Assert.Single(entries);
        Assert.Equal("e1", entries[0].Id);
        Assert.Equal("user", entries[0].Role);
        Assert.Equal("Hello!", entries[0].Content);
    }

    [Fact]
    public async Task AppendMultiple_PreservesOrder()
    {
        await _store.AppendAsync("s1", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "First"));
        await _store.AppendAsync("s1", new SessionEntry("e2", DateTimeOffset.UtcNow, "assistant", "Second"));

        var entries = await _store.LoadAsync("s1");

        Assert.Equal(2, entries.Count);
        Assert.Equal("e1", entries[0].Id);
        Assert.Equal("e2", entries[1].Id);
    }

    [Fact]
    public async Task Load_EmptySession_ReturnsEmpty()
    {
        var entries = await _store.LoadAsync("nonexistent");
        Assert.Empty(entries);
    }

    [Fact]
    public async Task ListAsync_ReturnsSessionIds()
    {
        await _store.AppendAsync("s1", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "A"));
        await _store.AppendAsync("s2", new SessionEntry("e2", DateTimeOffset.UtcNow, "user", "B"));

        var ids = await _store.ListAsync();

        Assert.Contains("s1", ids);
        Assert.Contains("s2", ids);
    }

    [Fact]
    public async Task Append_PreservesOptionalFields()
    {
        var entry = new SessionEntry("e1", DateTimeOffset.UtcNow, "assistant", "Reply")
        {
            Model = "claude-3",
            InputTokens = 100,
            OutputTokens = 50,
            ToolName = "Bash",
            ToolUseId = "tu-1",
            IsError = true,
        };
        await _store.AppendAsync("s1", entry);

        var loaded = (await _store.LoadAsync("s1"))[0];

        Assert.Equal("claude-3", loaded.Model);
        Assert.Equal(100, loaded.InputTokens);
        Assert.Equal(50, loaded.OutputTokens);
        Assert.Equal("Bash", loaded.ToolName);
        Assert.Equal("tu-1", loaded.ToolUseId);
        Assert.True(loaded.IsError);
    }

    [Fact]
    public async Task Append_NullOptionalFields_LoadsAsNull()
    {
        var entry = new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "Hi");
        await _store.AppendAsync("s1", entry);

        var loaded = (await _store.LoadAsync("s1"))[0];

        Assert.Null(loaded.Model);
        Assert.Null(loaded.ToolName);
        Assert.Null(loaded.ToolUseId);
        Assert.False(loaded.IsError);
    }

    // ── Phase 38 — owner scoping ─────────────────────────────────────────

    [Fact]
    public async Task AppendWithOwner_StampsSessionOwnerOnFirstTouch()
    {
        var entry = new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "hi");
        await _store.AppendAsync("s-alice", entry, ownerUserId: "usr-alice");

        Assert.Equal("usr-alice", await _store.GetOwnerAsync("s-alice"));
    }

    [Fact]
    public async Task AppendWithOwner_SecondAppendDoesNotOverwriteOwner()
    {
        await _store.AppendAsync("s-alice", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "hi"), "usr-alice");
        // A malicious second caller tries to hijack the session by re-appending with their id.
        await _store.AppendAsync("s-alice", new SessionEntry("e2", DateTimeOffset.UtcNow, "user", "hi again"), "usr-mallory");

        Assert.Equal("usr-alice", await _store.GetOwnerAsync("s-alice"));
    }

    [Fact]
    public async Task LoadAsync_NonOwner_ReturnsEmpty()
    {
        await _store.AppendAsync("s-bob", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "bob"), "usr-bob");

        var fromAlice = await _store.LoadAsync("s-bob", ownerUserId: "usr-alice");
        Assert.Empty(fromAlice);
    }

    [Fact]
    public async Task LoadAsync_Owner_ReturnsEntries()
    {
        await _store.AppendAsync("s-bob", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "bob"), "usr-bob");

        var fromBob = await _store.LoadAsync("s-bob", ownerUserId: "usr-bob");
        Assert.Single(fromBob);
    }

    [Fact]
    public async Task LoadAsync_NullFilter_ReturnsEntriesForAnyOwner()
    {
        await _store.AppendAsync("s-bob", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "bob"), "usr-bob");

        var all = await _store.LoadAsync("s-bob", ownerUserId: null);
        Assert.Single(all);
    }

    [Fact]
    public async Task ListAsync_Filters_ByOwner()
    {
        await _store.AppendAsync("s-a", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "a"), "usr-alice");
        await _store.AppendAsync("s-b", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "b"), "usr-bob");

        var aliceIds = await _store.ListAsync(ownerUserId: "usr-alice");
        Assert.Contains("s-a", aliceIds);
        Assert.DoesNotContain("s-b", aliceIds);

        var all = await _store.ListAsync(ownerUserId: null);
        Assert.Contains("s-a", all);
        Assert.Contains("s-b", all);
    }

    [Fact]
    public async Task DeleteAsync_NonOwner_ReturnsFalseAndKeepsData()
    {
        await _store.AppendAsync("s-bob", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "bob"), "usr-bob");

        var deleted = await _store.DeleteAsync("s-bob", ownerUserId: "usr-alice");
        Assert.False(deleted);

        // Data is still present under the real owner.
        Assert.Equal("usr-bob", await _store.GetOwnerAsync("s-bob"));
        Assert.Single(await _store.LoadAsync("s-bob", ownerUserId: "usr-bob"));
    }

    [Fact]
    public async Task DeleteAsync_Owner_ReturnsTrueAndRemovesData()
    {
        await _store.AppendAsync("s-bob", new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "bob"), "usr-bob");

        var deleted = await _store.DeleteAsync("s-bob", ownerUserId: "usr-bob");
        Assert.True(deleted);
        Assert.Null(await _store.GetOwnerAsync("s-bob"));
    }

    [Fact]
    public async Task GetOwnerAsync_UnknownSession_ReturnsNull()
    {
        Assert.Null(await _store.GetOwnerAsync("nope"));
    }
}
