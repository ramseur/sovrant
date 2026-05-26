using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.UserDashboard;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.UserDashboard;

public sealed class UserDashboardAggregatorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _missions;
    private readonly SqliteAgentRunStore _runs;
    private readonly IWorkspaceService _workspaces;
    private readonly UserDashboardAggregator _aggregator;

    public UserDashboardAggregatorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_userdash_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        var factory = (ISqliteConnectionFactory)_provider;
        _missions = new SqliteMissionStore(factory);
        _runs = new SqliteAgentRunStore(factory);
        _workspaces = new SqliteWorkspaceStore(factory);

        // Tests don't exercise the session path because LoadAsync requires
        // populated session_entries rows; we cover sessions implicitly via
        // the same IsVisible helper that the other sources use.
        var sessions = new SqliteSessionStore(factory);

        _aggregator = new UserDashboardAggregator(
            _missions,
            _runs,
            sessions,
            _workspaces,
            NullLogger<UserDashboardAggregator>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task User_SeesOwn_PublicAndPrivate_Missions()
    {
        await SeedUserAsync("alice");
        var aliceWs = await _workspaces.CreatePersonalWorkspaceAsync("alice");

        var pubMission = await _missions.CreateAsync("public goal", workspaceId: aliceWs.WorkspaceId, ownerUserId: "alice");
        await SetMissionPrivacyAsync(pubMission.Id, false);
        var privMission = await _missions.CreateAsync("private goal", workspaceId: aliceWs.WorkspaceId, ownerUserId: "alice");
        await SetMissionPrivacyAsync(privMission.Id, true);

        var state = await _aggregator.GetStateAsync("alice");

        Assert.Contains(state.Rows, r => r.Id == pubMission.Id && r.IsOwn && !r.IsPrivate);
        Assert.Contains(state.Rows, r => r.Id == privMission.Id && r.IsOwn && r.IsPrivate);
    }

    [Fact]
    public async Task User_SeesOtherUsers_PublicMission_InSharedWorkspace()
    {
        await SeedUserAsync("alice");
        await SeedUserAsync("bob");
        var teamWs = await _workspaces.CreateTeamWorkspaceAsync("shared", "shared", ownerId: "alice");
        await _workspaces.AddMemberAsync(teamWs.WorkspaceId, "bob", WorkspaceRole.Member);

        var bobMission = await _missions.CreateAsync("bob's public goal", workspaceId: teamWs.WorkspaceId, ownerUserId: "bob");
        await SetMissionPrivacyAsync(bobMission.Id, false);

        var state = await _aggregator.GetStateAsync("alice");

        Assert.Contains(state.Rows, r => r.Id == bobMission.Id && !r.IsOwn && !r.IsPrivate);
    }

    [Fact]
    public async Task User_DoesNotSee_OtherUsers_PrivateMission_EvenInSharedWorkspace()
    {
        await SeedUserAsync("alice");
        await SeedUserAsync("bob");
        var teamWs = await _workspaces.CreateTeamWorkspaceAsync("shared", "shared", ownerId: "alice");
        await _workspaces.AddMemberAsync(teamWs.WorkspaceId, "bob", WorkspaceRole.Member);

        var bobPriv = await _missions.CreateAsync("bob's secret", workspaceId: teamWs.WorkspaceId, ownerUserId: "bob");
        await SetMissionPrivacyAsync(bobPriv.Id, true);

        var state = await _aggregator.GetStateAsync("alice");

        Assert.DoesNotContain(state.Rows, r => r.Id == bobPriv.Id);
    }

    [Fact]
    public async Task User_DoesNotSee_Records_FromNonMemberWorkspace()
    {
        await SeedUserAsync("alice");
        await SeedUserAsync("bob");
        await _workspaces.CreatePersonalWorkspaceAsync("alice");
        var bobOnlyWs = await _workspaces.CreateTeamWorkspaceAsync("bob-only", "bob-only", ownerId: "bob");

        var bobMission = await _missions.CreateAsync("bob's public goal", workspaceId: bobOnlyWs.WorkspaceId, ownerUserId: "bob");
        await SetMissionPrivacyAsync(bobMission.Id, false);

        var state = await _aggregator.GetStateAsync("alice");

        Assert.DoesNotContain(state.Rows, r => r.Id == bobMission.Id);
    }

    [Fact]
    public async Task TeamRuns_FollowSamePrivacyRule_AsSoloAgentRuns()
    {
        await SeedUserAsync("alice");
        await SeedUserAsync("bob");
        var teamWs = await _workspaces.CreateTeamWorkspaceAsync("shared", "shared", ownerId: "alice");
        await _workspaces.AddMemberAsync(teamWs.WorkspaceId, "bob", WorkspaceRole.Member);

        var bobPublicTeam = new AgentRunRecord(
            RunId: "run-bob-public-team",
            ParentRunId: null,
            TeamId: "team-1",
            MemberId: null,
            WorkspaceId: teamWs.WorkspaceId,
            ProjectId: null,
            UserId: "bob",
            Kind: "delegation",
            Status: "running",
            StartedAt: DateTimeOffset.UtcNow,
            IsPrivate: false);
        var bobPrivateTeam = bobPublicTeam with
        {
            RunId = "run-bob-private-team",
            IsPrivate = true,
        };

        await _runs.CreateAsync(bobPublicTeam);
        await _runs.CreateAsync(bobPrivateTeam);

        var state = await _aggregator.GetStateAsync("alice");

        Assert.Contains(state.Rows, r => r.Id == "run-bob-public-team" && r.Kind == "team-run" && !r.IsOwn);
        Assert.DoesNotContain(state.Rows, r => r.Id == "run-bob-private-team");
    }

    [Fact]
    public async Task User_SeesOwn_PrivateAgentRun_Unmasked()
    {
        await SeedUserAsync("alice");
        var aliceWs = await _workspaces.CreatePersonalWorkspaceAsync("alice");

        var aliceRun = new AgentRunRecord(
            RunId: "run-alice-priv",
            ParentRunId: null,
            TeamId: null,
            MemberId: null,
            WorkspaceId: aliceWs.WorkspaceId,
            ProjectId: null,
            UserId: "alice",
            Kind: "delegation",
            Status: "running",
            StartedAt: DateTimeOffset.UtcNow,
            IsPrivate: true);

        await _runs.CreateAsync(aliceRun);

        var state = await _aggregator.GetStateAsync("alice");

        var row = Assert.Single(state.Rows, r => r.Id == "run-alice-priv");
        Assert.True(row.IsOwn);
        Assert.True(row.IsPrivate);
    }

    [Fact]
    public async Task Aggregator_ReturnsEmptyClaws_WithoutCrashing()
    {
        await SeedUserAsync("alice");
        await _workspaces.CreatePersonalWorkspaceAsync("alice");

        var state = await _aggregator.GetStateAsync("alice");

        Assert.Equal(0, state.Claws);
        Assert.DoesNotContain(state.Rows, r => r.Kind == "claw");
    }

    [Fact]
    public async Task GetStateAsync_NullOrEmptyUserId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _aggregator.GetStateAsync(""));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _aggregator.GetStateAsync(null!));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SeedUserSync(string userId)
    {
        using var connection = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO users (user_id, username, role, status, created_at, updated_at)
            VALUES ($id, $id, 'user', 'active',
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    private Task SeedUserAsync(string userId)
    {
        SeedUserSync(userId);
        return Task.CompletedTask;
    }

    private async Task SetMissionPrivacyAsync(string missionId, bool isPrivate)
    {
        using var connection = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE missions SET is_private = $p WHERE id = $id";
        cmd.Parameters.AddWithValue("$p", isPrivate ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", missionId);
        await cmd.ExecuteNonQueryAsync();
    }
}
