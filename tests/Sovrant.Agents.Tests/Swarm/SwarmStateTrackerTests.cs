using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class SwarmStateTrackerTests
{
    [Fact]
    public void Get_AfterUpdate_ReturnsResult()
    {
        var tracker = new SwarmStateTracker();
        var result = CreateResult("s1", SwarmStatus.Executing);
        tracker.Update("s1", result);

        Assert.Same(result, tracker.Get("s1"));
    }

    [Fact]
    public void Get_NotFound_ReturnsNull()
    {
        var tracker = new SwarmStateTracker();
        Assert.Null(tracker.Get("nonexistent"));
    }

    [Fact]
    public void GetLatest_ReturnsResultWithLongestDuration()
    {
        var tracker = new SwarmStateTracker();
        var r1 = CreateResult("s1", SwarmStatus.Completed, TimeSpan.FromSeconds(5));
        var r2 = CreateResult("s2", SwarmStatus.Completed, TimeSpan.FromSeconds(10));
        tracker.Update("s1", r1);
        tracker.Update("s2", r2);

        Assert.Same(r2, tracker.GetLatest());
    }

    [Fact]
    public void GetLatest_Empty_ReturnsNull()
    {
        var tracker = new SwarmStateTracker();
        Assert.Null(tracker.GetLatest());
    }

    [Fact]
    public void ListIds_ReturnsAllTrackedIds()
    {
        var tracker = new SwarmStateTracker();
        tracker.Update("s1", CreateResult("s1", SwarmStatus.Executing));
        tracker.Update("s2", CreateResult("s2", SwarmStatus.Completed));

        var ids = tracker.ListIds();
        Assert.Equal(2, ids.Count);
        Assert.Contains("s1", ids);
        Assert.Contains("s2", ids);
    }

    [Fact]
    public void Update_Overwrites_PreviousResult()
    {
        var tracker = new SwarmStateTracker();
        tracker.Update("s1", CreateResult("s1", SwarmStatus.Executing));
        var updated = CreateResult("s1", SwarmStatus.Completed);
        tracker.Update("s1", updated);

        Assert.Same(updated, tracker.Get("s1"));
        Assert.Equal(SwarmStatus.Completed, tracker.Get("s1")!.Status);
    }

    private static SwarmResult CreateResult(string id, SwarmStatus status, TimeSpan? duration = null)
    {
        return new SwarmResult
        {
            SwarmId = id,
            Status = status,
            Tasks = [],
            Duration = duration ?? TimeSpan.Zero,
        };
    }
}
