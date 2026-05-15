using Sovrant.Agents.Swarm;

namespace Sovrant.Agents.Tests.Swarm;

public class SwarmTaskNodeTests
{
    [Fact]
    public void DefaultStatus_IsPending()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        Assert.Equal(SwarmTaskStatus.Pending, node.Status);
    }

    [Fact]
    public void StatusTransition_PendingToRunning()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        node.Status = SwarmTaskStatus.Running;
        Assert.Equal(SwarmTaskStatus.Running, node.Status);
    }

    [Fact]
    public void StatusTransition_RunningToCompleted()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        node.Status = SwarmTaskStatus.Running;
        node.Status = SwarmTaskStatus.Completed;
        node.Output = "done";
        Assert.Equal(SwarmTaskStatus.Completed, node.Status);
        Assert.Equal("done", node.Output);
    }

    [Fact]
    public void StatusTransition_RunningToFailed()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        node.Status = SwarmTaskStatus.Failed;
        node.Error = "boom";
        Assert.Equal(SwarmTaskStatus.Failed, node.Status);
        Assert.Equal("boom", node.Error);
    }

    [Fact]
    public void Dependencies_DefaultsToEmpty()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        Assert.Empty(node.Dependencies);
    }

    [Fact]
    public void FilesToModify_DefaultsToEmpty()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        Assert.Empty(node.FilesToModify);
    }

    [Fact]
    public void Wave_CanBeAssigned()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        node.Wave = 3;
        Assert.Equal(3, node.Wave);
    }

    [Fact]
    public void TokensUsed_TracksUsage()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        node.TokensUsed = 1500;
        Assert.Equal(1500, node.TokensUsed);
    }

    [Fact]
    public void RetryCount_IncrementsCorrectly()
    {
        var node = new SwarmTaskNode { Id = "t1", Description = "Test" };
        Assert.Equal(0, node.RetryCount);
        node.RetryCount = 1;
        Assert.Equal(1, node.RetryCount);
    }
}
