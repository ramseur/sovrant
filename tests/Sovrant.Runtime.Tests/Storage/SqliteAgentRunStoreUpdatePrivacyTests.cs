using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteAgentRunStoreUpdatePrivacyTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteAgentRunStore _store;

    public SqliteAgentRunStoreUpdatePrivacyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_runs_priv_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteAgentRunStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static AgentRunRecord MakeRun(string userId = "alice", bool isPrivate = true) => new(
        RunId: $"run-{Guid.NewGuid():N}",
        ParentRunId: null,
        TeamId: null,
        MemberId: null,
        WorkspaceId: "ws-1",
        ProjectId: null,
        UserId: userId,
        Kind: "delegation",
        Status: "queued",
        StartedAt: DateTimeOffset.UtcNow,
        IsPrivate: isPrivate);

    [Fact]
    public async Task NewRun_DefaultsToPrivate()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);
        var got = await _store.GetAsync(run.RunId);
        Assert.NotNull(got);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task Owner_CanFlipToPublic()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);

        await _store.UpdatePrivacyAsync(run.RunId, "alice", isPrivate: false);

        var got = await _store.GetAsync(run.RunId);
        Assert.False(got!.IsPrivate);
    }

    [Fact]
    public async Task Owner_CanFlipBackToPrivate()
    {
        var run = MakeRun(isPrivate: false);
        await _store.CreateAsync(run);

        await _store.UpdatePrivacyAsync(run.RunId, "alice", isPrivate: true);

        var got = await _store.GetAsync(run.RunId);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task NonOwner_Update_IsSilentNoOp()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);

        await _store.UpdatePrivacyAsync(run.RunId, "mallory", isPrivate: false);

        var got = await _store.GetAsync(run.RunId);
        Assert.True(got!.IsPrivate);
    }

    [Fact]
    public async Task Idempotent_FlipSameValueTwice()
    {
        var run = MakeRun();
        await _store.CreateAsync(run);

        await _store.UpdatePrivacyAsync(run.RunId, "alice", isPrivate: true);
        await _store.UpdatePrivacyAsync(run.RunId, "alice", isPrivate: true);

        var got = await _store.GetAsync(run.RunId);
        Assert.True(got!.IsPrivate);
    }
}
