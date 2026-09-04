using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class SqliteWorkflowStoreUpdatePrivacyTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;

    public SqliteWorkflowStoreUpdatePrivacyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_missions_priv_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task NewWorkflow_DefaultsToPrivate()
    {
        var m = await _store.CreateAsync("goal", ownerUserId: "alice");
        var got = await _store.GetAsync(m.Id);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task Owner_CanFlipToPublic()
    {
        var m = await _store.CreateAsync("goal", ownerUserId: "alice");
        await _store.UpdatePrivacyAsync(m.Id, "alice", isPrivate: false);
        var got = await _store.GetAsync(m.Id);
        Assert.False(got!.IsPrivate);
    }

    [Fact]
    public async Task Owner_CanFlipBackToPrivate()
    {
        var m = await _store.CreateAsync("goal", ownerUserId: "alice");
        await _store.UpdatePrivacyAsync(m.Id, "alice", isPrivate: false);
        await _store.UpdatePrivacyAsync(m.Id, "alice", isPrivate: true);
        var got = await _store.GetAsync(m.Id);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task NonOwner_Update_IsSilentNoOp()
    {
        var m = await _store.CreateAsync("goal", ownerUserId: "alice");
        await _store.UpdatePrivacyAsync(m.Id, "mallory", isPrivate: false);
        var got = await _store.GetAsync(m.Id);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task Idempotent_FlipSameValueTwice()
    {
        var m = await _store.CreateAsync("goal", ownerUserId: "alice");
        await _store.UpdatePrivacyAsync(m.Id, "alice", isPrivate: true);
        await _store.UpdatePrivacyAsync(m.Id, "alice", isPrivate: true);
        var got = await _store.GetAsync(m.Id);
        Assert.True(got!.IsPrivate);
    }
}
