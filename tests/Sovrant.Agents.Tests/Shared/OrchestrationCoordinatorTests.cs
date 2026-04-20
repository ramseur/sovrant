using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Config;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;

namespace Sovrant.Agents.Tests.Shared;

public sealed class OrchestrationCoordinatorTests : IDisposable
{
    private readonly AgentSystemConfig _config = new() { MaxConcurrentAgents = 2, TaskTimeoutSeconds = 5 };
    private readonly OrchestrationCoordinator _coordinator;

    public OrchestrationCoordinatorTests()
    {
        _coordinator = new OrchestrationCoordinator(_config, NullLogger<OrchestrationCoordinator>.Instance);
    }

    public void Dispose() => _coordinator.Dispose();

    [Fact]
    public async Task DispatchAsync_Routes_To_Named_Agent()
    {
        var agent = new FakeAgent("alpha");
        _coordinator.AddAgent(agent);

        var task = AgentTask.Create("hello", "alpha");
        var result = await _coordinator.DispatchAsync(task);

        Assert.True(result.Success);
        Assert.Equal("Handled: hello", result.Output);
    }

    [Fact]
    public async Task DispatchAsync_Unknown_Agent_Returns_Fail()
    {
        var task = AgentTask.Create("hello", "nonexistent");
        var result = await _coordinator.DispatchAsync(task);

        Assert.False(result.Success);
        Assert.Contains("Unknown agent", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_No_Agents_Returns_Fail()
    {
        var task = AgentTask.Create("hello");
        var result = await _coordinator.DispatchAsync(task);

        Assert.False(result.Success);
        Assert.Contains("No agents registered", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsync_Falls_Back_To_First_Agent()
    {
        _coordinator.AddAgent(new FakeAgent("first"));
        _coordinator.AddAgent(new FakeAgent("second"));

        var task = AgentTask.Create("test"); // No assigned agent
        var result = await _coordinator.DispatchAsync(task);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DispatchAsync_Timeout_Returns_Fail()
    {
        var config = new AgentSystemConfig { TaskTimeoutSeconds = 1 };
        using var coordinator = new OrchestrationCoordinator(config, NullLogger<OrchestrationCoordinator>.Instance);

        var slowAgent = new SlowAgent("slow", delay: TimeSpan.FromSeconds(10));
        coordinator.AddAgent(slowAgent);

        var task = AgentTask.Create("wait forever", "slow");
        var result = await coordinator.DispatchAsync(task);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DispatchAsync_Cancellation_Returns_Fail()
    {
        var slowAgent = new SlowAgent("slow", delay: TimeSpan.FromSeconds(60));
        _coordinator.AddAgent(slowAgent);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var task = AgentTask.Create("wait", "slow");
        var result = await _coordinator.DispatchAsync(task, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancel_Cancels_InFlight_Task()
    {
        var slowAgent = new SlowAgent("slow", delay: TimeSpan.FromSeconds(60));
        _coordinator.AddAgent(slowAgent);

        var task = AgentTask.Create("wait", "slow");
        var dispatchTask = _coordinator.DispatchAsync(task);

        // Give dispatch time to start
        await Task.Delay(50).ConfigureAwait(true);
        _coordinator.Cancel(task.Id);

        var result = await dispatchTask;
        Assert.False(result.Success);
    }

    [Fact]
    public async Task MaxConcurrentAgents_Enforces_Limit()
    {
        var config = new AgentSystemConfig { MaxConcurrentAgents = 1, TaskTimeoutSeconds = 10 };
        using var coordinator = new OrchestrationCoordinator(config, NullLogger<OrchestrationCoordinator>.Instance);

        var trackingAgent = new TrackingAgent("track");
        coordinator.AddAgent(trackingAgent);

        var task1 = coordinator.DispatchAsync(AgentTask.Create("a", "track"));
        var task2 = coordinator.DispatchAsync(AgentTask.Create("b", "track"));

        await Task.WhenAll(task1, task2).ConfigureAwait(true);

        // With MaxConcurrentAgents=1, max observed concurrency should be 1
        Assert.Equal(1, trackingAgent.MaxObservedConcurrency);
    }

    [Fact]
    public async Task ShutdownAsync_Completes()
    {
        _coordinator.AddAgent(new FakeAgent("agent"));
        await _coordinator.ShutdownAsync();
        // Should not throw
    }

    private sealed class FakeAgent : IAgent
    {
        public string Name { get; }
        public FakeAgent(string name) => Name = name;

        public Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default) =>
            Task.FromResult(AgentResult.Ok(task.Id, $"Handled: {task.Prompt}"));
    }

    private sealed class SlowAgent : IAgent
    {
        private readonly TimeSpan _delay;
        public string Name { get; }

        public SlowAgent(string name, TimeSpan delay)
        {
            Name = name;
            _delay = delay;
        }

        public async Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default)
        {
            await Task.Delay(_delay, ct).ConfigureAwait(true);
            return AgentResult.Ok(task.Id, "done");
        }
    }

    private sealed class TrackingAgent : IAgent
    {
        private int _currentConcurrency;
        public int MaxObservedConcurrency { get; private set; }
        public string Name { get; }

        public TrackingAgent(string name) => Name = name;

        public async Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            if (current > MaxObservedConcurrency)
                MaxObservedConcurrency = current;

            await Task.Delay(100, ct).ConfigureAwait(true);

            Interlocked.Decrement(ref _currentConcurrency);
            return AgentResult.Ok(task.Id, "tracked");
        }
    }
}
