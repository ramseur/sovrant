using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

/// <summary>Phase 133 — conversation folders on SQLite (V048).</summary>
public sealed class SqliteSessionFolderStoreTests : IAsyncDisposable
{
    private const string Me = "me@example.com";
    private const string Other = "other@example.com";

    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISessionFolderStore _folders;
    private readonly ISessionStore _sessions;

    public SqliteSessionFolderStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _folders = new SqliteSessionFolderStore(_provider);
        _sessions = new SqliteSessionStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private Task Conversation(string sessionId, string owner = Me) =>
        _sessions.AppendAsync(sessionId, new SessionEntry(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, "user", "hi"), owner);

    private async Task<string?> FolderOf(string sessionId) =>
        (await _sessions.ListWithTitlesAsync()).Single(s => s.SessionId == sessionId).FolderId;

    [Fact]
    public async Task Create_NestsFolders_AndListReturnsTheWholeTree()
    {
        var client = await _folders.CreateAsync(Me, "Client A", null);
        var proposals = await _folders.CreateAsync(Me, "Proposals", client.FolderId);

        var tree = await _folders.ListAsync(Me);

        Assert.Equal(2, tree.Count);
        Assert.Null(tree.Single(f => f.FolderId == client.FolderId).ParentFolderId);
        Assert.Equal(client.FolderId, tree.Single(f => f.FolderId == proposals.FolderId).ParentFolderId);
    }

    [Fact]
    public async Task Create_RefusesASixthLevel()
    {
        string? parent = null;
        for (var level = 1; level <= SessionFolderRules.MaxDepth; level++)
            parent = (await _folders.CreateAsync(Me, $"L{level}", parent)).FolderId;

        var ex = await Assert.ThrowsAsync<SessionFolderException>(() => _folders.CreateAsync(Me, "L6", parent));
        Assert.Equal(SessionFolderError.TooDeep, ex.Error);
    }

    [Fact]
    public async Task Create_RefusesDuplicateSiblingIgnoringCase_ButAllowsItUnderAnotherParent()
    {
        var research = await _folders.CreateAsync(Me, "Research", null);

        var ex = await Assert.ThrowsAsync<SessionFolderException>(() => _folders.CreateAsync(Me, "research", null));
        Assert.Equal(SessionFolderError.DuplicateName, ex.Error);

        var nested = await _folders.CreateAsync(Me, "Research", research.FolderId);
        Assert.Equal("Research", nested.Name);
    }

    [Fact]
    public async Task Folders_ArePerUser()
    {
        await _folders.CreateAsync(Me, "Mine", null);
        var theirs = await _folders.CreateAsync(Other, "Mine", null); // same name, different owner — fine

        Assert.Single(await _folders.ListAsync(Me));
        await Assert.ThrowsAsync<SessionFolderException>(() => _folders.RenameAsync(Me, theirs.FolderId, "Stolen"));
        await Assert.ThrowsAsync<SessionFolderException>(() => _folders.CreateAsync(Me, "Child", theirs.FolderId));
        Assert.False(await _folders.DeleteAsync(Me, theirs.FolderId));
    }

    [Fact]
    public async Task Rename_ChecksSiblingNames()
    {
        var a = await _folders.CreateAsync(Me, "Alpha", null);
        await _folders.CreateAsync(Me, "Beta", null);

        var ex = await Assert.ThrowsAsync<SessionFolderException>(() => _folders.RenameAsync(Me, a.FolderId, "BETA"));
        Assert.Equal(SessionFolderError.DuplicateName, ex.Error);

        var renamed = await _folders.RenameAsync(Me, a.FolderId, "  Gamma ");
        Assert.Equal("Gamma", renamed.Name);
    }

    [Fact]
    public async Task Move_RefusesMovingAFolderIntoItsOwnSubfolder()
    {
        var parent = await _folders.CreateAsync(Me, "Client A", null);
        var child = await _folders.CreateAsync(Me, "Proposals", parent.FolderId);

        var ex = await Assert.ThrowsAsync<SessionFolderException>(() => _folders.MoveAsync(Me, parent.FolderId, child.FolderId));
        Assert.Equal(SessionFolderError.Cycle, ex.Error);
    }

    [Fact]
    public async Task Move_RefusesWhenTheSubtreeWouldPassTheDepthLimit()
    {
        var top = await _folders.CreateAsync(Me, "Top", null);
        string? parent = null;
        var chain = new List<SessionFolder>();
        for (var level = 1; level <= 5; level++)
        {
            var f = await _folders.CreateAsync(Me, $"Deep{level}", parent);
            chain.Add(f);
            parent = f.FolderId;
        }

        // A 5-level chain under a top-level folder would reach level 6.
        var ex = await Assert.ThrowsAsync<SessionFolderException>(() => _folders.MoveAsync(Me, chain[0].FolderId, top.FolderId));
        Assert.Equal(SessionFolderError.TooDeep, ex.Error);

        // Its 4-level lower part fits exactly (deepest lands on level 5).
        var moved = await _folders.MoveAsync(Me, chain[1].FolderId, top.FolderId);
        Assert.Equal(top.FolderId, moved.ParentFolderId);
    }

    [Fact]
    public async Task Move_ToTopLevel_Works()
    {
        var parent = await _folders.CreateAsync(Me, "Client A", null);
        var child = await _folders.CreateAsync(Me, "Proposals", parent.FolderId);

        var moved = await _folders.MoveAsync(Me, child.FolderId, null);

        Assert.Null(moved.ParentFolderId);
    }

    [Fact]
    public async Task Delete_MovesConversationsAndSubfoldersUp_AndDeletesNoConversation()
    {
        var client = await _folders.CreateAsync(Me, "Client A", null);
        var proposals = await _folders.CreateAsync(Me, "Proposals", client.FolderId);
        var drafts = await _folders.CreateAsync(Me, "Drafts", proposals.FolderId);
        await Conversation("s-in-proposals");
        await Conversation("s-in-drafts");
        await _folders.MoveSessionAsync(Me, "s-in-proposals", proposals.FolderId);
        await _folders.MoveSessionAsync(Me, "s-in-drafts", drafts.FolderId);

        Assert.True(await _folders.DeleteAsync(Me, proposals.FolderId));

        var tree = await _folders.ListAsync(Me);
        Assert.DoesNotContain(tree, f => f.FolderId == proposals.FolderId);
        Assert.Equal(client.FolderId, tree.Single(f => f.FolderId == drafts.FolderId).ParentFolderId);
        Assert.Equal(client.FolderId, await FolderOf("s-in-proposals"));
        Assert.Equal(drafts.FolderId, await FolderOf("s-in-drafts"));
        Assert.Equal(2, (await _sessions.ListAsync(Me)).Count);
    }

    [Fact]
    public async Task Delete_RenamesASubfolderWhoseNameClashesAtTheNewLevel()
    {
        var client = await _folders.CreateAsync(Me, "Client A", null);
        await _folders.CreateAsync(Me, "Notes", client.FolderId);
        var archive = await _folders.CreateAsync(Me, "Archive", client.FolderId);
        var innerNotes = await _folders.CreateAsync(Me, "Notes", archive.FolderId);

        await _folders.DeleteAsync(Me, archive.FolderId);

        var moved = (await _folders.ListAsync(Me)).Single(f => f.FolderId == innerNotes.FolderId);
        Assert.Equal(client.FolderId, moved.ParentFolderId);
        Assert.Equal("Notes (2)", moved.Name);
    }

    [Fact]
    public async Task Delete_TopLevelFolder_UnfilesItsConversations()
    {
        var folder = await _folders.CreateAsync(Me, "Scratch", null);
        await Conversation("s1");
        await _folders.MoveSessionAsync(Me, "s1", folder.FolderId);

        await _folders.DeleteAsync(Me, folder.FolderId);

        Assert.Null(await FolderOf("s1"));
    }

    [Fact]
    public async Task MoveSession_FilesAndUnfiles_AndListWithTitlesReportsTheFolder()
    {
        var folder = await _folders.CreateAsync(Me, "Research", null);
        await Conversation("s1");

        Assert.True(await _folders.MoveSessionAsync(Me, "s1", folder.FolderId));
        Assert.Equal(folder.FolderId, await FolderOf("s1"));

        Assert.True(await _folders.MoveSessionAsync(Me, "s1", null));
        Assert.Null(await FolderOf("s1"));
    }

    [Fact]
    public async Task MoveSession_RefusesOtherUsersConversationsAndFolders()
    {
        var mine = await _folders.CreateAsync(Me, "Mine", null);
        var theirs = await _folders.CreateAsync(Other, "Theirs", null);
        await Conversation("my-chat", Me);
        await Conversation("their-chat", Other);

        Assert.False(await _folders.MoveSessionAsync(Me, "their-chat", mine.FolderId));
        await Assert.ThrowsAsync<SessionFolderException>(() => _folders.MoveSessionAsync(Me, "my-chat", theirs.FolderId));
        Assert.False(await _folders.MoveSessionAsync(Me, "no-such-chat", null));
        Assert.Null(await FolderOf("their-chat"));
    }
}
