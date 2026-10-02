using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Tests.Session;

/// <summary>Phase 133 — the pure folder-tree rules shared by both stores and both sidebars.</summary>
public sealed class SessionFolderRulesTests
{
    private static SessionFolder F(string id, string? parent, string name) =>
        new(id, "u1", parent, name, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    // a ─ b ─ c ─ d ─ e   (a chain 5 deep)   x (separate top-level folder)
    private static readonly IReadOnlyList<SessionFolder> Chain =
    [
        F("a", null, "A"), F("b", "a", "B"), F("c", "b", "C"), F("d", "c", "D"), F("e", "d", "E"), F("x", null, "X"),
    ];

    [Fact]
    public void DepthOf_CountsLevelsFromTop()
    {
        Assert.Equal(0, SessionFolderRules.DepthOf(Chain, null));
        Assert.Equal(1, SessionFolderRules.DepthOf(Chain, "a"));
        Assert.Equal(5, SessionFolderRules.DepthOf(Chain, "e"));
    }

    [Fact]
    public void SubtreeHeight_CountsTheFolderItself()
    {
        Assert.Equal(5, SessionFolderRules.SubtreeHeight(Chain, "a"));
        Assert.Equal(1, SessionFolderRules.SubtreeHeight(Chain, "e"));
    }

    [Fact]
    public void CheckCreate_RefusesSixthLevel()
    {
        Assert.Equal(SessionFolderError.TooDeep, SessionFolderRules.CheckCreate(Chain, "e", "Sixth"));
        Assert.Null(SessionFolderRules.CheckCreate(Chain, "d", "Fifth sibling"));
    }

    [Fact]
    public void CheckCreate_RefusesDuplicateSiblingIgnoringCase_ButAllowsSameNameElsewhere()
    {
        Assert.Equal(SessionFolderError.DuplicateName, SessionFolderRules.CheckCreate(Chain, null, "x"));
        Assert.Null(SessionFolderRules.CheckCreate(Chain, "a", "X"));
    }

    [Fact]
    public void CheckCreate_RefusesUnknownParent() =>
        Assert.Equal(SessionFolderError.NotFound, SessionFolderRules.CheckCreate(Chain, "nope", "New"));

    [Theory]
    [InlineData("a", "a")]   // into itself
    [InlineData("a", "c")]   // into a descendant
    [InlineData("b", "e")]
    public void CheckMove_RefusesCycles(string folder, string newParent) =>
        Assert.Equal(SessionFolderError.Cycle, SessionFolderRules.CheckMove(Chain, folder, newParent));

    [Fact]
    public void CheckMove_RefusesWhenSubtreeWouldPassDepthLimit()
    {
        // b's subtree is 4 levels (b,c,d,e); under x (level 1) its deepest folder would be level 5 — allowed.
        Assert.Null(SessionFolderRules.CheckMove(Chain, "b", "x"));
        // a's subtree is 5 levels; under x it would reach level 6.
        Assert.Equal(SessionFolderError.TooDeep, SessionFolderRules.CheckMove(Chain, "a", "x"));
    }

    [Fact]
    public void CheckMove_ToTopLevel_IsAllowed() =>
        Assert.Null(SessionFolderRules.CheckMove(Chain, "c", null));

    [Fact]
    public void CheckMove_RefusesNameClashAtDestination()
    {
        IReadOnlyList<SessionFolder> folders = [F("p", null, "Notes"), F("q", null, "Work"), F("r", "q", "notes")];
        Assert.Equal(SessionFolderError.DuplicateName, SessionFolderRules.CheckMove(folders, "r", null));
    }

    [Fact]
    public void UniqueName_AddsTheFirstFreeSuffix()
    {
        IReadOnlyList<SessionFolder> folders = [F("p", null, "Notes"), F("q", null, "Notes (2)")];
        Assert.Equal("Notes (3)", SessionFolderRules.UniqueName(folders, null, "Notes"));
        Assert.Equal("Fresh", SessionFolderRules.UniqueName(folders, null, "Fresh"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nname")]
    public void NormalizeName_RejectsUnusableNames(string name)
    {
        var ex = Assert.Throws<SessionFolderException>(() => SessionFolderRules.NormalizeName(name));
        Assert.Equal(SessionFolderError.InvalidName, ex.Error);
    }

    [Fact]
    public void NormalizeName_TrimsAndEnforcesLength()
    {
        Assert.Equal("Client A", SessionFolderRules.NormalizeName("  Client A  "));
        Assert.Throws<SessionFolderException>(() => SessionFolderRules.NormalizeName(new string('x', SessionFolderRules.MaxNameLength + 1)));
    }
}
