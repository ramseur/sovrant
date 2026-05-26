using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteSessionStoreUpdatePrivacyTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISessionStore _store;

    public SqliteSessionStoreUpdatePrivacyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_session_priv_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteSessionStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task SeedAsync(string sessionId, string ownerUserId)
    {
        await _store.AppendAsync(sessionId,
            new SessionEntry("e1", DateTimeOffset.UtcNow, "user", "hi"),
            ownerUserId: ownerUserId);
    }

    [Fact]
    public async Task NewSession_DefaultsToPrivate()
    {
        await SeedAsync("s1", "alice");
        Assert.True(await _store.GetIsPrivateAsync("s1"));
    }

    [Fact]
    public async Task Owner_CanFlipToPublic()
    {
        await SeedAsync("s1", "alice");
        await _store.UpdatePrivacyAsync("s1", "alice", isPrivate: false);
        Assert.False(await _store.GetIsPrivateAsync("s1"));
    }

    [Fact]
    public async Task Owner_CanFlipBackToPrivate()
    {
        await SeedAsync("s1", "alice");
        await _store.UpdatePrivacyAsync("s1", "alice", isPrivate: false);
        await _store.UpdatePrivacyAsync("s1", "alice", isPrivate: true);
        Assert.True(await _store.GetIsPrivateAsync("s1"));
    }

    [Fact]
    public async Task NonOwner_Update_IsSilentNoOp()
    {
        await SeedAsync("s1", "alice");
        await _store.UpdatePrivacyAsync("s1", "mallory", isPrivate: false);
        Assert.True(await _store.GetIsPrivateAsync("s1"));
    }

    [Fact]
    public async Task Idempotent_FlipSameValueTwice()
    {
        await SeedAsync("s1", "alice");
        await _store.UpdatePrivacyAsync("s1", "alice", isPrivate: true);
        await _store.UpdatePrivacyAsync("s1", "alice", isPrivate: true);
        Assert.True(await _store.GetIsPrivateAsync("s1"));
    }
}
