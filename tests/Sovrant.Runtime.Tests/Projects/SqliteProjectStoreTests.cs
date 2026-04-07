using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Projects;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Projects;

public sealed class SqliteProjectStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IWorkspaceService _workspaceService;
    private readonly IProjectService _projectService;
    private readonly string _userId;
    private readonly string _workspaceId;

    public SqliteProjectStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_proj_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        var factory = (ISqliteConnectionFactory)_provider;
        _workspaceService = new SqliteWorkspaceStore(factory);
        _projectService = new SqliteProjectStore(factory, _workspaceService);

        _userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID") ?? Environment.UserName;

        // The default user and personal workspace are seeded by SqliteStorageProvider.
        // Create a team workspace for tests.
        var ws = _workspaceService.CreateTeamWorkspaceAsync("Test Team", $"test-team-{Guid.NewGuid():N}", _userId)
            .GetAwaiter().GetResult();
        _workspaceId = ws.WorkspaceId;
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void SeedUser(string userId)
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

    // ── CRUD ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAndGet_RoundTrips()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "My Project", "my-proj", "A test project");

        Assert.Equal("My Project", proj.Name);
        Assert.Equal("my-proj", proj.Slug);
        Assert.Equal("A test project", proj.Description);
        Assert.Equal(_workspaceId, proj.WorkspaceId);
        Assert.Null(proj.ArchivedAt);

        var loaded = await _projectService.GetAsync(proj.ProjectId);
        Assert.NotNull(loaded);
        Assert.Equal("My Project", loaded.Name);
    }

    [Fact]
    public async Task List_ReturnsProjectsInWorkspace()
    {
        await _projectService.CreateAsync(_workspaceId, "Alpha", "alpha-list");
        await _projectService.CreateAsync(_workspaceId, "Beta", "beta-list");

        var projects = await _projectService.ListAsync(_workspaceId);
        Assert.True(projects.Count >= 2);
        Assert.Contains(projects, p => p.Slug == "alpha-list");
        Assert.Contains(projects, p => p.Slug == "beta-list");
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Old", "update-test");
        var updated = await _projectService.UpdateAsync(proj.ProjectId, name: "New", description: "Updated desc");

        Assert.NotNull(updated);
        Assert.Equal("New", updated.Name);
        Assert.Equal("Updated desc", updated.Description);
        Assert.Equal("update-test", updated.Slug); // unchanged
    }

    [Fact]
    public async Task Delete_RemovesProject()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Delete Me", "delete-proj");

        var deleted = await _projectService.DeleteAsync(proj.ProjectId);
        Assert.True(deleted);

        var loaded = await _projectService.GetAsync(proj.ProjectId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsFalse()
    {
        var deleted = await _projectService.DeleteAsync("nonexistent");
        Assert.False(deleted);
    }

    // ── Archive ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAndUnarchive()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Archivable", "archive-test");

        var archived = await _projectService.ArchiveAsync(proj.ProjectId);
        Assert.True(archived);

        var loaded = await _projectService.GetAsync(proj.ProjectId);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.ArchivedAt);

        // Archived projects excluded from list by default.
        var list = await _projectService.ListAsync(_workspaceId);
        Assert.DoesNotContain(list, p => p.ProjectId == proj.ProjectId);

        // Included when includeArchived = true.
        var listAll = await _projectService.ListAsync(_workspaceId, includeArchived: true);
        Assert.Contains(listAll, p => p.ProjectId == proj.ProjectId);

        // Unarchive.
        var unarchived = await _projectService.UnarchiveAsync(proj.ProjectId);
        Assert.True(unarchived);

        loaded = await _projectService.GetAsync(proj.ProjectId);
        Assert.NotNull(loaded);
        Assert.Null(loaded.ArchivedAt);
    }

    // ── Membership ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HasAccess_OpenByDefault()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Open", "open-access");

        // Owner of workspace has access (no explicit project members = open).
        var hasAccess = await _projectService.HasAccessAsync(proj.ProjectId, _userId);
        Assert.True(hasAccess);
    }

    [Fact]
    public async Task HasAccess_RestrictedWhenMembersExist()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Restricted", "restricted-access");

        // Add an explicit member.
        await _projectService.AddMemberAsync(proj.ProjectId, _userId, ProjectRole.Lead);

        // The explicit member has access.
        Assert.True(await _projectService.HasAccessAsync(proj.ProjectId, _userId));

        // A non-member workspace user does NOT have access.
        SeedUser("outsider");
        await _workspaceService.AddMemberAsync(_workspaceId, "outsider", WorkspaceRole.Member);
        Assert.False(await _projectService.HasAccessAsync(proj.ProjectId, "outsider"));
    }

    [Fact]
    public async Task AddMember_RequiresWorkspaceMembership()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "WS Check", "ws-check");

        // Non-workspace-member can't be added.
        SeedUser("nonwsmember");
        var added = await _projectService.AddMemberAsync(proj.ProjectId, "nonwsmember", ProjectRole.Contributor);
        Assert.False(added);
    }

    [Fact]
    public async Task ListAndRemoveMembers()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Members", "members-test");
        await _projectService.AddMemberAsync(proj.ProjectId, _userId, ProjectRole.Lead);

        var members = await _projectService.ListMembersAsync(proj.ProjectId);
        Assert.Single(members);
        Assert.Equal(ProjectRole.Lead, members[0].Role);

        var removed = await _projectService.RemoveMemberAsync(proj.ProjectId, _userId);
        Assert.True(removed);

        members = await _projectService.ListMembersAsync(proj.ProjectId);
        Assert.Empty(members);
    }

    [Fact]
    public async Task NonWorkspaceMember_HasNoAccess()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Isolated", "isolated-proj");

        SeedUser("stranger");
        Assert.False(await _projectService.HasAccessAsync(proj.ProjectId, "stranger"));
    }

    // ── Config ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAndGetConfig()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Config", "config-proj");

        await _projectService.SetConfigAsync(proj.ProjectId, new Dictionary<string, string>
        {
            ["model"] = "gpt-4o",
            ["budget"] = "100",
        });

        var config = await _projectService.GetConfigAsync(proj.ProjectId);
        Assert.Equal(2, config.Count);
        Assert.Equal("gpt-4o", config["model"]);
    }

    [Fact]
    public async Task GetResolvedConfig_InheritanceChain()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Inherit", "inherit-proj");

        // Set workspace config.
        await _workspaceService.SetConfigAsync(_workspaceId, new Dictionary<string, string>
        {
            ["model"] = "gpt-4",
            ["theme"] = "dark",
        });

        // Set project config (overrides workspace model).
        await _projectService.SetConfigAsync(proj.ProjectId, new Dictionary<string, string>
        {
            ["model"] = "gpt-4o",
        });

        var resolved = await _projectService.GetResolvedConfigAsync(proj.ProjectId);
        Assert.Equal("gpt-4o", resolved["model"]); // project overrides workspace
        Assert.Equal("dark", resolved["theme"]);    // inherited from workspace
    }

    // ── Memory ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMemory_MergesProjectAndWorkspaceMemory()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Memory", "memory-proj");

        // Save workspace-level memory (no project_id).
        await _workspaceService.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "ws-mem-1",
            WorkspaceId = _workspaceId,
            Layer = "pattern",
            Content = "workspace pattern",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        // Save project-scoped memory.
        await _workspaceService.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "proj-mem-1",
            WorkspaceId = _workspaceId,
            Layer = "pattern",
            Content = "project pattern",
            ProjectId = proj.ProjectId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var memory = await _projectService.GetMemoryAsync(proj.ProjectId);
        Assert.Equal(2, memory.Count);
        Assert.Contains(memory, m => m.Content == "workspace pattern");
        Assert.Contains(memory, m => m.Content == "project pattern");
    }

    [Fact]
    public async Task GetMemory_IsolatedAcrossProjects()
    {
        var proj1 = await _projectService.CreateAsync(_workspaceId, "P1", "iso-p1");
        var proj2 = await _projectService.CreateAsync(_workspaceId, "P2", "iso-p2");

        await _workspaceService.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "p1-only",
            WorkspaceId = _workspaceId,
            Layer = "instinct",
            Content = "p1 only",
            ProjectId = proj1.ProjectId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var p1Mem = await _projectService.GetMemoryAsync(proj1.ProjectId);
        var p2Mem = await _projectService.GetMemoryAsync(proj2.ProjectId);

        Assert.Contains(p1Mem, m => m.Content == "p1 only");
        Assert.DoesNotContain(p2Mem, m => m.Content == "p1 only");
    }

    // ── Usage ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsage_ReturnsZerosWhenEmpty()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Usage", "usage-proj");
        var usage = await _projectService.GetUsageAsync(proj.ProjectId);

        Assert.Equal(0, usage.TotalInputTokens);
        Assert.Equal(0, usage.TotalOutputTokens);
        Assert.Equal(0, usage.SessionCount);
    }

    // ── Sessions ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListSessions_ReturnsEmptyWhenNoSessions()
    {
        var proj = await _projectService.CreateAsync(_workspaceId, "Sessions", "sessions-proj");
        var sessions = await _projectService.ListSessionsAsync(proj.ProjectId);
        Assert.Empty(sessions);
    }
}
