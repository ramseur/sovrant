using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Teams;

public sealed class SqliteTeamRegistryTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteTeamRegistry _registry;

    public SqliteTeamRegistryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_team_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _registry = new SqliteTeamRegistry(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static TeamInfo MakeTeam(string? name = null) => new(
        Id: $"team-{Guid.NewGuid():N}",
        WorkspaceId: "ws-1",
        ProjectId: null,
        Name: name ?? "test-team",
        Description: "a test team",
        Origin: "user",
        CreatedBy: "alice",
        CreatedAt: DateTimeOffset.UtcNow);

    private static TeamMemberInfo MakeMember(string teamId, string? name = null) => new()
    {
        Id = $"member-{Guid.NewGuid():N}",
        TeamId = teamId,
        WorkspaceId = "ws-1",
        Name = name ?? "test-member",
        Role = AgentRole.Coder,
        SystemPrompt = "you are a coder",
        Template = "coder",
        CreatedBy = "alice",
    };

    // ── Team CRUD ───────────────────────────────────────────────────────

    [Fact]
    public void CreateTeam_And_GetTeam_RoundTrips()
    {
        var team = MakeTeam("my-team");
        _registry.CreateTeam(team);

        var got = _registry.GetTeam(team.Id);

        Assert.NotNull(got);
        Assert.Equal("my-team", got.Name);
        Assert.Equal("ws-1", got.WorkspaceId);
        Assert.Equal("user", got.Origin);
        Assert.Equal("alice", got.CreatedBy);
    }

    [Fact]
    public void GetTeam_Unknown_ReturnsNull()
    {
        Assert.Null(_registry.GetTeam("nonexistent"));
    }

    [Fact]
    public void ListTeams_FiltersByWorkspace()
    {
        var t1 = MakeTeam("team-a");
        var t2 = new TeamInfo($"team-{Guid.NewGuid():N}", "ws-other", null, "team-b", null, "user", "bob", DateTimeOffset.UtcNow);
        _registry.CreateTeam(t1);
        _registry.CreateTeam(t2);

        var all = _registry.ListTeams();
        var ws1Only = _registry.ListTeams("ws-1");

        Assert.Equal(2, all.Count);
        Assert.Single(ws1Only);
        Assert.Equal("team-a", ws1Only[0].Name);
    }

    [Fact]
    public void RemoveTeam_DeletesTeamAndMembers()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);
        _registry.RegisterMember(MakeMember(team.Id, "worker-1"));
        _registry.RegisterMember(MakeMember(team.Id, "worker-2"));

        Assert.True(_registry.RemoveTeam(team.Id));
        Assert.Null(_registry.GetTeam(team.Id));
        Assert.Empty(_registry.GetTeamMembers(team.Id));
    }

    [Fact]
    public void RemoveTeam_Unknown_ReturnsFalse()
    {
        Assert.False(_registry.RemoveTeam("nope"));
    }

    // ── Member CRUD ─────────────────────────────────────────────────────

    [Fact]
    public void RegisterMember_And_GetMember_RoundTrips()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);
        var member = MakeMember(team.Id, "coder-1");

        _registry.RegisterMember(member);
        var got = _registry.GetMember(member.Id);

        Assert.NotNull(got);
        Assert.Equal("coder-1", got.Name);
        Assert.Equal(AgentRole.Coder, got.Role);
        Assert.Equal("coder", got.Template);
        Assert.Equal(team.Id, got.TeamId);
    }

    [Fact]
    public void GetTeamMembers_ReturnsOnlyTeamMembers()
    {
        var t1 = MakeTeam("team-a");
        var t2 = MakeTeam("team-b");
        _registry.CreateTeam(t1);
        _registry.CreateTeam(t2);

        _registry.RegisterMember(MakeMember(t1.Id, "a-worker"));
        _registry.RegisterMember(MakeMember(t2.Id, "b-worker"));

        var t1Members = _registry.GetTeamMembers(t1.Id);
        Assert.Single(t1Members);
        Assert.Equal("a-worker", t1Members[0].Name);
    }

    [Fact]
    public void GetAllMembers_ReturnsAll()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);
        _registry.RegisterMember(MakeMember(team.Id, "m1"));
        _registry.RegisterMember(MakeMember(team.Id, "m2"));

        var all = _registry.GetAllMembers();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void RemoveMember_Works()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);
        var member = MakeMember(team.Id);
        _registry.RegisterMember(member);

        Assert.True(_registry.RemoveMember(member.Id));
        Assert.Null(_registry.GetMember(member.Id));
    }

    [Fact]
    public void MemberWithAllowedTools_Persists()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);

        var member = new TeamMemberInfo
        {
            Id = $"member-{Guid.NewGuid():N}",
            TeamId = team.Id,
            WorkspaceId = "ws-1",
            Name = "tool-user",
            Role = AgentRole.General,
            SystemPrompt = "test",
            AllowedTools = ["ReadFile", "WriteFile", "Bash"],
            CreatedBy = "alice",
        };
        _registry.RegisterMember(member);

        var got = _registry.GetMember(member.Id);
        Assert.NotNull(got);
        Assert.NotNull(got.AllowedTools);
        Assert.Equal(3, got.AllowedTools.Count);
        Assert.Contains("ReadFile", got.AllowedTools);
    }

    // ── Survival across recreation ──────────────────────────────────────

    [Fact]
    public void DataSurvives_RegistryRecreation()
    {
        var team = MakeTeam("persistent");
        _registry.CreateTeam(team);
        _registry.RegisterMember(MakeMember(team.Id, "survivor"));

        // Create a new registry pointing at the same DB
        var registry2 = new SqliteTeamRegistry(_provider);

        var got = registry2.GetTeam(team.Id);
        Assert.NotNull(got);
        Assert.Equal("persistent", got.Name);

        var members = registry2.GetTeamMembers(team.Id);
        Assert.Single(members);
        Assert.Equal("survivor", members[0].Name);
    }

    // ── Run profile (Phase 78 Path 2) ───────────────────────────────────

    [Fact]
    public void CreateTeam_DefaultRunProfile_IsParallel()
    {
        // Phase 78 Path 2 commit 7 — new teams default to parallel
        // execution so picking Team isn't a throughput downgrade vs. Swarm.
        // Safety rails (file locks, quality gate) remain opt-in.
        var team = MakeTeam();
        _registry.CreateTeam(team);

        var got = _registry.GetTeam(team.Id);
        Assert.NotNull(got);
        Assert.Equal(TeamRunMode.Parallel, got.RunMode);
        Assert.Equal(4, got.MaxConcurrent);
        Assert.False(got.FileLocksEnabled);
        Assert.False(got.QualityGateEnabled);
        Assert.Equal(7, got.QualityGateThreshold);
        Assert.Equal(TeamDecompositionMode.Off, got.DecompositionMode);
    }

    [Fact]
    public void CreateTeam_PersistsExplicitRunProfile()
    {
        var team = MakeTeam() with
        {
            RunMode = TeamRunMode.Swarm,
            MaxConcurrent = 4,
            FileLocksEnabled = true,
            QualityGateEnabled = true,
            QualityGateThreshold = 8,
            DecompositionMode = TeamDecompositionMode.RoleAware,
        };
        _registry.CreateTeam(team);

        var got = _registry.GetTeam(team.Id);
        Assert.NotNull(got);
        Assert.Equal(TeamRunMode.Swarm, got.RunMode);
        Assert.Equal(4, got.MaxConcurrent);
        Assert.True(got.FileLocksEnabled);
        Assert.True(got.QualityGateEnabled);
        Assert.Equal(8, got.QualityGateThreshold);
        Assert.Equal(TeamDecompositionMode.RoleAware, got.DecompositionMode);
    }

    [Fact]
    public void UpdateTeamRunProfile_MutatesPersistedFields()
    {
        var team = MakeTeam();
        _registry.CreateTeam(team);

        var profile = new TeamRunProfile(
            RunMode: TeamRunMode.Parallel,
            MaxConcurrent: 3,
            FileLocksEnabled: true,
            QualityGateEnabled: false,
            QualityGateThreshold: 6,
            DecompositionMode: TeamDecompositionMode.Open);

        Assert.True(_registry.UpdateTeamRunProfile(team.Id, profile));

        var got = _registry.GetTeam(team.Id);
        Assert.NotNull(got);
        Assert.Equal(TeamRunMode.Parallel, got.RunMode);
        Assert.Equal(3, got.MaxConcurrent);
        Assert.True(got.FileLocksEnabled);
        Assert.False(got.QualityGateEnabled);
        Assert.Equal(6, got.QualityGateThreshold);
        Assert.Equal(TeamDecompositionMode.Open, got.DecompositionMode);
    }

    [Fact]
    public void UpdateTeamRunProfile_Unknown_ReturnsFalse()
    {
        var profile = new TeamRunProfile(
            TeamRunMode.Sequential, 1, false, false, 7, TeamDecompositionMode.Off);
        Assert.False(_registry.UpdateTeamRunProfile("nonexistent", profile));
    }
}
