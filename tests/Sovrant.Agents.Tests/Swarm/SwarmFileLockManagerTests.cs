using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class SwarmFileLockManagerTests
{
    [Fact]
    public void TryAcquire_FirstLock_ReturnsTrue()
    {
        var mgr = new SwarmFileLockManager();
        Assert.True(mgr.TryAcquire("/src/foo.cs", "task-01"));
    }

    [Fact]
    public void TryAcquire_SameTask_ReturnsTrue()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        Assert.True(mgr.TryAcquire("/src/foo.cs", "task-01"));
    }

    [Fact]
    public void TryAcquire_DifferentTask_ReturnsFalse()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        Assert.False(mgr.TryAcquire("/src/foo.cs", "task-02"));
    }

    [Fact]
    public void IsLockedByOther_WhenLockedByAnother_ReturnsTrue()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        Assert.True(mgr.IsLockedByOther("/src/foo.cs", "task-02"));
    }

    [Fact]
    public void IsLockedByOther_WhenLockedBySelf_ReturnsFalse()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        Assert.False(mgr.IsLockedByOther("/src/foo.cs", "task-01"));
    }

    [Fact]
    public void IsLockedByOther_WhenUnlocked_ReturnsFalse()
    {
        var mgr = new SwarmFileLockManager();
        Assert.False(mgr.IsLockedByOther("/src/foo.cs", "task-01"));
    }

    [Fact]
    public void GetHolder_ReturnsTaskId()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        Assert.Equal("task-01", mgr.GetHolder("/src/foo.cs"));
    }

    [Fact]
    public void GetHolder_WhenUnlocked_ReturnsNull()
    {
        var mgr = new SwarmFileLockManager();
        Assert.Null(mgr.GetHolder("/src/foo.cs"));
    }

    [Fact]
    public void ReleaseAll_RemovesLocksForTask()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        mgr.TryAcquire("/src/bar.cs", "task-01");
        mgr.TryAcquire("/src/baz.cs", "task-02");

        mgr.ReleaseAll("task-01");

        Assert.Null(mgr.GetHolder("/src/foo.cs"));
        Assert.Null(mgr.GetHolder("/src/bar.cs"));
        Assert.Equal("task-02", mgr.GetHolder("/src/baz.cs"));
    }

    [Fact]
    public void Clear_RemovesAllLocks()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        mgr.TryAcquire("/src/bar.cs", "task-02");

        mgr.Clear();

        Assert.Null(mgr.GetHolder("/src/foo.cs"));
        Assert.Null(mgr.GetHolder("/src/bar.cs"));
    }

    [Fact]
    public void PathNormalization_CaseInsensitive()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/SRC/Foo.cs", "task-01");
        Assert.True(mgr.IsLockedByOther("/src/foo.cs", "task-02"));
    }

    [Fact]
    public void PathNormalization_BackslashToForwardSlash()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("src\\foo.cs", "task-01");
        Assert.Equal("task-01", mgr.GetHolder("src/foo.cs"));
    }

    [Fact]
    public void Snapshot_ReturnsCurrentLocks()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/a.cs", "task-01");
        mgr.TryAcquire("/src/b.cs", "task-02");

        var snap = mgr.Snapshot();
        Assert.Equal(2, snap.Count);
    }

    [Fact]
    public void AfterRelease_CanAcquireByAnotherTask()
    {
        var mgr = new SwarmFileLockManager();
        mgr.TryAcquire("/src/foo.cs", "task-01");
        mgr.ReleaseAll("task-01");
        Assert.True(mgr.TryAcquire("/src/foo.cs", "task-02"));
    }
}
