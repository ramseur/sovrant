using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Workspaces;

public sealed class SqliteWorkspaceStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IWorkspaceService _store;

    public SqliteWorkspaceStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_ws_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkspaceStore((ISqliteConnectionFactory)_provider);
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

    // ── Personal workspace auto-creation ──────────────────────────────────

    [Fact]
    public async Task SeedDefaultUser_CreatesPersonalWorkspace()
    {
        // The default user seeded by SqliteStorageProvider should have a personal workspace.
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.GetPersonalAsync(userId);
        Assert.NotNull(ws);
        Assert.Equal(WorkspaceType.Personal, ws.Type);
        Assert.Equal(userId, ws.OwnerId);
    }

    [Fact]
    public async Task CreatePersonalWorkspace_Idempotent()
    {
        // Create the user first (FK constraint).
        SeedUser("testuser1");

        var ws1 = await _store.CreatePersonalWorkspaceAsync("testuser1");
        var ws2 = await _store.CreatePersonalWorkspaceAsync("testuser1");
        Assert.Equal(ws1.WorkspaceId, ws2.WorkspaceId);
    }

    // ── Team workspace CRUD ───────────────────────────────────────────────

    [Fact]
    public async Task CreateAndGetTeamWorkspace()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("My Team", "my-team", userId);

        Assert.Equal(WorkspaceType.Team, ws.Type);
        Assert.Equal("My Team", ws.Name);
        Assert.Equal("my-team", ws.Slug);

        var loaded = await _store.GetAsync(ws.WorkspaceId);
        Assert.NotNull(loaded);
        Assert.Equal("My Team", loaded.Name);
    }

    [Fact]
    public async Task ListForUser_IncludesPersonalAndTeam()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        await _store.CreateTeamWorkspaceAsync("Team A", "team-a-list", userId);

        var workspaces = await _store.ListForUserAsync(userId);

        // At least personal + the team we just created.
        Assert.True(workspaces.Count >= 2);
        Assert.Contains(workspaces, w => w.Type == WorkspaceType.Personal);
        Assert.Contains(workspaces, w => w.Slug == "team-a-list");
    }

    [Fact]
    public async Task UpdateWorkspace_ChangesName()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Old Name", "update-test", userId);

        var updated = await _store.UpdateAsync(ws.WorkspaceId, "New Name", null);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("update-test", updated.Slug); // slug unchanged
    }

    [Fact]
    public async Task DeleteTeamWorkspace_Succeeds()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Delete Me", "delete-test", userId);

        var deleted = await _store.DeleteAsync(ws.WorkspaceId);
        Assert.True(deleted);

        var loaded = await _store.GetAsync(ws.WorkspaceId);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeletePersonalWorkspace_Fails()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var personal = await _store.GetPersonalAsync(userId);
        Assert.NotNull(personal);

        var deleted = await _store.DeleteAsync(personal.WorkspaceId);
        Assert.False(deleted);
    }

    // ── Membership ─────────────────────────────────────────────────────────

    [Fact]
    public async Task OwnerIsMember()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Member Test", "member-test", userId);

        Assert.True(await _store.IsMemberAsync(ws.WorkspaceId, userId));
        Assert.Equal(WorkspaceRole.Owner, await _store.GetMemberRoleAsync(ws.WorkspaceId, userId));
    }

    [Fact]
    public async Task AddAndRemoveMember()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Add Rm Test", "add-rm-test", userId);

        SeedUser("user2");

        await _store.AddMemberAsync(ws.WorkspaceId, "user2", WorkspaceRole.Member);
        Assert.True(await _store.IsMemberAsync(ws.WorkspaceId, "user2"));

        var members = await _store.ListMembersAsync(ws.WorkspaceId);
        Assert.Equal(2, members.Count);

        var removed = await _store.RemoveMemberAsync(ws.WorkspaceId, "user2");
        Assert.True(removed);
        Assert.False(await _store.IsMemberAsync(ws.WorkspaceId, "user2"));
    }

    [Fact]
    public async Task CannotRemoveOwner()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Owner Rm Test", "owner-rm-test", userId);

        var removed = await _store.RemoveMemberAsync(ws.WorkspaceId, userId);
        Assert.False(removed);
    }

    // ── Isolation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task NonMember_IsNotMember()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Isolation Test", "isolation-test", userId);

        Assert.False(await _store.IsMemberAsync(ws.WorkspaceId, "nonexistent-user"));
    }

    // ── Invites ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAndAcceptInvite()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Invite Test", "invite-test", userId);

        var invite = await _store.CreateInviteAsync(ws.WorkspaceId, "test@example.com", WorkspaceRole.Member);
        Assert.NotNull(invite.Token);
        Assert.Null(invite.AcceptedAt);

        SeedUser("invitee");

        var accepted = await _store.AcceptInviteAsync(invite.Token, "invitee");
        Assert.True(accepted);
        Assert.True(await _store.IsMemberAsync(ws.WorkspaceId, "invitee"));

        // Cannot accept again.
        var again = await _store.AcceptInviteAsync(invite.Token, "invitee");
        Assert.False(again);
    }

    [Fact]
    public async Task DeleteInvite_Succeeds()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Inv Del", "inv-del-test", userId);

        var invite = await _store.CreateInviteAsync(ws.WorkspaceId, "del@test.com", WorkspaceRole.Viewer);
        var deleted = await _store.DeleteInviteAsync(invite.InviteId);
        Assert.True(deleted);

        var invites = await _store.ListInvitesAsync(ws.WorkspaceId);
        Assert.Empty(invites);
    }

    // ── Config ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAndGetConfig()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Config Test", "config-test", userId);

        await _store.SetConfigAsync(ws.WorkspaceId, new Dictionary<string, string>
        {
            ["model"] = "gpt-4",
            ["theme"] = "dark",
        });

        var config = await _store.GetConfigAsync(ws.WorkspaceId);
        Assert.Equal(2, config.Count);
        Assert.Equal("gpt-4", config["model"]);
        Assert.Equal("dark", config["theme"]);
    }

    [Fact]
    public async Task SetConfig_OverwritesExisting()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Config Overwrite", "config-overwrite", userId);

        await _store.SetConfigAsync(ws.WorkspaceId, new Dictionary<string, string> { ["key"] = "v1" });
        await _store.SetConfigAsync(ws.WorkspaceId, new Dictionary<string, string> { ["key"] = "v2" });

        var config = await _store.GetConfigAsync(ws.WorkspaceId);
        Assert.Equal("v2", config["key"]);
    }

    // ── Workspace memory ──────────────────────────────────────────────────

    [Fact]
    public async Task SaveAndListMemory()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Memory Test", "memory-test", userId);

        var entry = new WorkspaceMemoryEntry
        {
            MemoryId = "mem-1",
            WorkspaceId = ws.WorkspaceId,
            Layer = "pattern",
            Content = "Use async/await everywhere",
            Confidence = 0.9,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _store.SaveMemoryAsync(entry);

        var memory = await _store.ListMemoryAsync(ws.WorkspaceId);
        Assert.Single(memory);
        Assert.Equal("Use async/await everywhere", memory[0].Content);
    }

    [Fact]
    public async Task ListMemory_FiltersByLayer()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Layer Filter", "layer-filter", userId);

        await _store.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "mem-p1", WorkspaceId = ws.WorkspaceId,
            Layer = "pattern", Content = "pattern1",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _store.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "mem-s1", WorkspaceId = ws.WorkspaceId,
            Layer = "summary", Content = "summary1",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });

        var patterns = await _store.ListMemoryAsync(ws.WorkspaceId, "pattern");
        Assert.Single(patterns);
        Assert.Equal("pattern1", patterns[0].Content);
    }

    [Fact]
    public async Task DeleteMemory_Removes()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Mem Delete", "mem-delete", userId);

        await _store.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "mem-del", WorkspaceId = ws.WorkspaceId,
            Layer = "instinct", Content = "test instinct",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });

        var deleted = await _store.DeleteMemoryAsync("mem-del");
        Assert.True(deleted);

        var memory = await _store.ListMemoryAsync(ws.WorkspaceId);
        Assert.Empty(memory);
    }

    [Fact]
    public async Task MemoryIsolated_AcrossWorkspaces()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws1 = await _store.CreateTeamWorkspaceAsync("Iso1", "iso-1", userId);
        var ws2 = await _store.CreateTeamWorkspaceAsync("Iso2", "iso-2", userId);

        await _store.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "mem-iso1", WorkspaceId = ws1.WorkspaceId,
            Layer = "pattern", Content = "ws1 only",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        });

        var ws1Mem = await _store.ListMemoryAsync(ws1.WorkspaceId);
        var ws2Mem = await _store.ListMemoryAsync(ws2.WorkspaceId);
        Assert.Single(ws1Mem);
        Assert.Empty(ws2Mem);
    }

    // ── Usage ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsage_ReturnsZerosWhenEmpty()
    {
        var userId = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        var ws = await _store.CreateTeamWorkspaceAsync("Usage Test", "usage-test", userId);

        var usage = await _store.GetUsageAsync(ws.WorkspaceId);
        Assert.Equal(0, usage.TotalInputTokens);
        Assert.Equal(0, usage.TotalOutputTokens);
        Assert.Equal(0, usage.SessionCount);
    }
}
