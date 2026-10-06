using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Tests.Session;

/// <summary>Phase 133 — the sidebar rows both Web and Desktop render.</summary>
public sealed class SessionFolderTreeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static SessionFolder F(string id, string? parent, string name) => new(id, "u1", parent, name, 0, T0, T0);

    private static SessionListItem S(string id, string? folder, int minutesAgo, string? title = null, params SessionLabel[] labels) =>
        new(id, title ?? id, T0.AddMinutes(-minutesAgo), FolderId: folder, Labels: labels);

    // Client A ─ Proposals          Research (empty)
    private static readonly IReadOnlyList<SessionFolder> Folders =
        [F("client", null, "Client A"), F("proposals", "client", "Proposals"), F("research", null, "Research")];

    private static readonly IReadOnlyList<SessionListItem> Sessions =
    [
        S("draft", "proposals", 5, "Proposal draft v2", new SessionLabel("Agent · proposal-writer")),
        S("pricing", "proposals", 30, "Pricing comparison"),
        S("kickoff", "client", 60, "Kickoff notes"),
        S("weekly", "client", 10, "Weekly status", new SessionLabel("Workflow · Running", IsActive: true)),
        S("loose", null, 1, "Fix login bug"),
        S("orphan", "deleted-folder", 2, "Orphaned"),
    ];

    [Fact]
    public void CollapsedTree_ShowsTopLevelFoldersOnly_WithDeepCounts()
    {
        var rows = SessionFolderTree.FolderRows(Folders, Sessions, new HashSet<string>());

        Assert.Equal(["Client A", "Research"], rows.Select(r => r.Text));
        Assert.Equal(4, rows[0].Count);          // 2 in Proposals + 2 directly in Client A
        Assert.True(rows[0].HasChildren);
        Assert.False(rows[1].HasChildren);
    }

    [Fact]
    public void ExpandedTree_ListsSubfoldersFirst_ThenConversationsNewestFirst()
    {
        var rows = SessionFolderTree.FolderRows(Folders, Sessions, new HashSet<string> { "client", "proposals" });

        Assert.Equal(
            ["Client A", "Proposals", "Proposal draft v2", "Pricing comparison", "Weekly status", "Kickoff notes", "Research"],
            rows.Select(r => r.Text));
        Assert.Equal([0, 1, 2, 2, 1, 1, 0], rows.Select(r => r.Depth));
        var weekly = rows.Single(r => r.Id == "weekly");
        Assert.Equal("Workflow · Running", weekly.Meta);
        Assert.True(weekly.MetaActive);
    }

    [Fact]
    public void Unfiled_IncludesConversationsWhoseFolderNoLongerExists()
    {
        var rows = SessionFolderTree.UnfiledRows(Folders, Sessions);
        Assert.Equal(["loose", "orphan"], rows.Select(r => r.Id));
    }

    [Fact]
    public void Unfiled_IsCapped() =>
        Assert.Single(SessionFolderTree.UnfiledRows(Folders, Sessions, limit: 1));

    [Fact]
    public void Search_CoversEveryFolder_AndShowsThePath()
    {
        var rows = SessionFolderTree.SearchRows(Folders, Sessions, "pric");

        var hit = Assert.Single(rows);
        Assert.Equal("Pricing comparison", hit.Text);
        Assert.Equal("Client A › Proposals", hit.Meta);
        Assert.Equal("Unfiled", SessionFolderTree.SearchRows(Folders, Sessions, "login").Single().Meta);
    }

    [Fact]
    public void PathAndAncestors()
    {
        Assert.Equal("Client A › Proposals", SessionFolderTree.PathOf(Folders, "proposals"));
        Assert.Equal(["client", "proposals"], SessionFolderTree.AncestorsOf(Folders, "proposals"));
        Assert.Equal(string.Empty, SessionFolderTree.PathOf(Folders, null));
    }

    [Fact]
    public void PickerRows_DisableOwnSubtreeWhenMovingAFolder()
    {
        var rows = SessionFolderTree.PickerRows(Folders, movingFolderId: "client");

        Assert.Equal(SessionFolderError.Cycle, rows.Single(r => r.Folder.FolderId == "client").Refusal);
        Assert.Equal(SessionFolderError.Cycle, rows.Single(r => r.Folder.FolderId == "proposals").Refusal);
        Assert.Null(rows.Single(r => r.Folder.FolderId == "research").Refusal);
    }

    [Fact]
    public async Task FallbackTitles_UseTheFirstMessage_OnlyForUntitledConversations()
    {
        var store = new FakeStore();
        store.Entries["untitled"] = [new SessionEntry("1", T0, "assistant", "Workflow created: tidy the backlog")];
        store.Entries["titled"] = [new SessionEntry("2", T0, "user", "should not be read")];
        IReadOnlyList<SessionListItem> items = [new("untitled", null, T0), new("titled", "Kept", T0)];

        var titled = await SessionFolderTree.WithFallbackTitlesAsync(store, items, "u1");

        Assert.Equal("Workflow created: tidy the backlog", titled.Single(i => i.SessionId == "untitled").Title);
        Assert.Equal("Kept", titled.Single(i => i.SessionId == "titled").Title);
        Assert.Equal(["untitled"], store.Loaded);
    }

    private sealed class FakeStore : ISessionStore
    {
        public Dictionary<string, IReadOnlyList<SessionEntry>> Entries { get; } = [];
        public List<string> Loaded { get; } = [];

        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
        {
            Loaded.Add(sessionId);
            return Task.FromResult(Entries.GetValueOrDefault(sessionId) ?? []);
        }

        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> DeleteAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
