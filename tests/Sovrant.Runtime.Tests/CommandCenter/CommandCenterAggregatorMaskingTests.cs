using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.CommandCenter;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.CommandCenter;

/// <summary>
/// Phase 99 — non-owner viewers (typically admins on the Command Center view)
/// see private rows with title masked to "(private)" and content/detail link
/// stripped. Owners see their own private rows unredacted.
/// </summary>
public sealed class CommandCenterAggregatorMaskingTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _missions;
    private readonly SqliteAgentRunStore _runs;
    private readonly CommandCenterAggregator _aggregator;

    public CommandCenterAggregatorMaskingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_cc_mask_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        var factory = (ISqliteConnectionFactory)_provider;

        _missions = new SqliteMissionStore(factory);
        _runs = new SqliteAgentRunStore(factory);
        var sessions = new SqliteSessionStore(factory);

        _aggregator = new CommandCenterAggregator(
            _missions, _runs, sessions,
            NullLogger<CommandCenterAggregator>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task NonOwnerViewer_OnPrivateMission_SeesMaskedRow()
    {
        var mission = await _missions.CreateAsync("alice's secret plan", ownerUserId: "alice");
        // Default is private (verified by other tests); leave as-is.

        // Admin view: ownerUserId = null (no owner filter), viewerUserId = "bob".
        var state = await _aggregator.GetActiveStateAsync(ownerUserId: null, viewerUserId: "bob");

        var row = Assert.Single(state.Rows, r => r.Id == mission.Id);
        Assert.True(row.IsPrivate);
        Assert.True(row.IsMasked);
        Assert.Equal("(private)", row.Title);
        Assert.Null(row.DetailRoute);
        Assert.Null(row.Preview);
    }

    [Fact]
    public async Task OwnerViewer_OnOwnPrivateMission_SeesUnredactedRow()
    {
        var mission = await _missions.CreateAsync("alice's secret plan", ownerUserId: "alice");

        var state = await _aggregator.GetActiveStateAsync(ownerUserId: null, viewerUserId: "alice");

        var row = Assert.Single(state.Rows, r => r.Id == mission.Id);
        Assert.True(row.IsPrivate);
        Assert.False(row.IsMasked);
        Assert.Contains("alice's secret plan", row.Title);
        Assert.NotNull(row.DetailRoute);
    }

    [Fact]
    public async Task PublicMission_IsNeverMasked()
    {
        var mission = await _missions.CreateAsync("alice's open plan", ownerUserId: "alice");
        await _missions.UpdatePrivacyAsync(mission.Id, "alice", isPrivate: false);

        var state = await _aggregator.GetActiveStateAsync(ownerUserId: null, viewerUserId: "bob");

        var row = Assert.Single(state.Rows, r => r.Id == mission.Id);
        Assert.False(row.IsPrivate);
        Assert.False(row.IsMasked);
        Assert.Contains("alice's open plan", row.Title);
    }

    [Fact]
    public async Task NonOwnerViewer_OnPrivateAgentRun_SeesMaskedRow()
    {
        var run = new AgentRunRecord(
            RunId: $"run-{Guid.NewGuid():N}",
            ParentRunId: null,
            TeamId: null,
            MemberId: "researcher",
            WorkspaceId: "ws-1",
            ProjectId: null,
            UserId: "alice",
            Kind: "delegation",
            Status: "running",
            StartedAt: DateTimeOffset.UtcNow,
            IsPrivate: true);
        await _runs.CreateAsync(run);

        var state = await _aggregator.GetActiveStateAsync(ownerUserId: null, viewerUserId: "bob");

        var row = Assert.Single(state.Rows, r => r.Id == run.RunId);
        Assert.True(row.IsPrivate);
        Assert.True(row.IsMasked);
        Assert.Equal("(private)", row.Title);
        Assert.Null(row.DetailRoute);
    }

    [Fact]
    public async Task OwnerViewer_OnOwnPrivateAgentRun_SeesUnredactedRow()
    {
        var run = new AgentRunRecord(
            RunId: $"run-{Guid.NewGuid():N}",
            ParentRunId: null,
            TeamId: null,
            MemberId: "researcher",
            WorkspaceId: "ws-1",
            ProjectId: null,
            UserId: "alice",
            Kind: "delegation",
            Status: "running",
            StartedAt: DateTimeOffset.UtcNow,
            IsPrivate: true);
        await _runs.CreateAsync(run);

        var state = await _aggregator.GetActiveStateAsync(ownerUserId: null, viewerUserId: "alice");

        var row = Assert.Single(state.Rows, r => r.Id == run.RunId);
        Assert.True(row.IsPrivate);
        Assert.False(row.IsMasked);
        Assert.Contains("researcher", row.Title);
        Assert.NotNull(row.DetailRoute);
    }
}
