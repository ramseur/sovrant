using Sovrant.Runtime.Workflows;

namespace Sovrant.Runtime.Tests.Workflows;

/// <summary>Tests for <see cref="IAutonomousDriver"/>, <see cref="LlmAutonomousDriver"/>, and <see cref="DriverRegistry"/>.</summary>
public sealed class AutonomousDriverTests
{
    private static Workflow MakeWorkflow(string id = "m-1") =>
        new(id, "goal", WorkflowStatus.Planning, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeWorkflowExecutor : IWorkflowExecutor
    {
        public int Calls { get; private set; }
        public string? LastWorkflowId { get; private set; }
        public Workflow Result { get; set; } = MakeWorkflow();

        public Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            Calls++;
            LastWorkflowId = workflowId;
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task LlmDriver_Advance_DelegatesTo_Executor()
    {
        var fake = new FakeWorkflowExecutor { Result = MakeWorkflow("abc") };
        var driver = new LlmAutonomousDriver(fake);

        var m = await driver.AdvanceAsync("abc");

        Assert.Equal(1, fake.Calls);
        Assert.Equal("abc", fake.LastWorkflowId);
        Assert.Equal("abc", m.Id);
    }

    [Fact]
    public void LlmDriver_Name_IsStable()
    {
        var driver = new LlmAutonomousDriver(new FakeWorkflowExecutor());
        Assert.Equal("llm", driver.Name);
        Assert.Equal(LlmAutonomousDriver.DriverName, driver.Name);
    }

    [Fact]
    public void LlmDriver_Capabilities_MatchDocumentedShape()
    {
        var driver = new LlmAutonomousDriver(new FakeWorkflowExecutor());
        Assert.True(driver.Capabilities.SupportsReplanning);
        Assert.False(driver.Capabilities.SupportsParallelSteps);
        Assert.True(driver.Capabilities.SupportsHumanAcceptance);
        Assert.Equal(1, driver.Capabilities.MaxStepsPerCycle);
    }

    [Fact]
    public void DriverRegistry_TryGet_IsCaseInsensitive()
    {
        var driver = new LlmAutonomousDriver(new FakeWorkflowExecutor());
        var registry = new DriverRegistry([driver]);

        Assert.Same(driver, registry.TryGet("llm"));
        Assert.Same(driver, registry.TryGet("LLM"));
        Assert.Same(driver, registry.TryGet("Llm"));
    }

    [Fact]
    public void DriverRegistry_TryGet_ReturnsNull_ForUnknown()
    {
        var registry = new DriverRegistry([new LlmAutonomousDriver(new FakeWorkflowExecutor())]);
        Assert.Null(registry.TryGet("does-not-exist"));
    }

    [Fact]
    public void DriverRegistry_All_ExposesRegisteredDrivers()
    {
        var llm = new LlmAutonomousDriver(new FakeWorkflowExecutor());
        var registry = new DriverRegistry([llm]);
        Assert.Single(registry.All);
        Assert.Contains(llm, registry.All);
    }
}
