using Sovrant.Runtime.Missions;

namespace Sovrant.Runtime.Tests.Missions;

/// <summary>Tests for <see cref="IAutonomousDriver"/>, <see cref="LlmAutonomousDriver"/>, and <see cref="DriverRegistry"/>.</summary>
public sealed class AutonomousDriverTests
{
    private static Mission MakeMission(string id = "m-1") =>
        new(id, "goal", MissionStatus.Planning, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class FakeMissionExecutor : IMissionExecutor
    {
        public int Calls { get; private set; }
        public string? LastMissionId { get; private set; }
        public Mission Result { get; set; } = MakeMission();

        public Task<Mission> RunAsync(string missionId, CancellationToken ct = default)
        {
            Calls++;
            LastMissionId = missionId;
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task LlmDriver_Advance_DelegatesTo_Executor()
    {
        var fake = new FakeMissionExecutor { Result = MakeMission("abc") };
        var driver = new LlmAutonomousDriver(fake);

        var m = await driver.AdvanceAsync("abc");

        Assert.Equal(1, fake.Calls);
        Assert.Equal("abc", fake.LastMissionId);
        Assert.Equal("abc", m.Id);
    }

    [Fact]
    public void LlmDriver_Name_IsStable()
    {
        var driver = new LlmAutonomousDriver(new FakeMissionExecutor());
        Assert.Equal("llm", driver.Name);
        Assert.Equal(LlmAutonomousDriver.DriverName, driver.Name);
    }

    [Fact]
    public void LlmDriver_Capabilities_MatchDocumentedShape()
    {
        var driver = new LlmAutonomousDriver(new FakeMissionExecutor());
        Assert.True(driver.Capabilities.SupportsReplanning);
        Assert.False(driver.Capabilities.SupportsParallelSteps);
        Assert.True(driver.Capabilities.SupportsHumanAcceptance);
        Assert.Equal(1, driver.Capabilities.MaxStepsPerCycle);
    }

    [Fact]
    public void DriverRegistry_TryGet_IsCaseInsensitive()
    {
        var driver = new LlmAutonomousDriver(new FakeMissionExecutor());
        var registry = new DriverRegistry([driver]);

        Assert.Same(driver, registry.TryGet("llm"));
        Assert.Same(driver, registry.TryGet("LLM"));
        Assert.Same(driver, registry.TryGet("Llm"));
    }

    [Fact]
    public void DriverRegistry_TryGet_ReturnsNull_ForUnknown()
    {
        var registry = new DriverRegistry([new LlmAutonomousDriver(new FakeMissionExecutor())]);
        Assert.Null(registry.TryGet("does-not-exist"));
    }

    [Fact]
    public void DriverRegistry_All_ExposesRegisteredDrivers()
    {
        var llm = new LlmAutonomousDriver(new FakeMissionExecutor());
        var registry = new DriverRegistry([llm]);
        Assert.Single(registry.All);
        Assert.Contains(llm, registry.All);
    }
}
