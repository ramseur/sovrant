using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class WorkflowPlanningServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;

    public WorkflowPlanningServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_workflow_planning_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private sealed class TwoStepPlanner : IWorkflowPlanner
    {
        public Task<RuntimePlan> PlanAsync(Workflow mission, CancellationToken ct = default) =>
            Task.FromResult(new RuntimePlan("plan-x", 1, mission.Goal,
            [
                new RuntimeStep(0, "step one", "one done", RuntimeModelTier.Standard),
                new RuntimeStep(1, "step two", "two done", RuntimeModelTier.Fast),
            ], DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task GenerateAsync_CreatesWorkflowAndLandsInAwaitingHumanWithPlan()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner());

        var workflow = await service.GenerateAsync("build something");

        Assert.Equal(WorkflowStatus.AwaitingHuman, workflow.Status);
        var plan = WorkflowPlanJson.TryDeserialize(workflow.PlanJson, workflow.Goal);
        Assert.NotNull(plan);
        Assert.Equal(2, plan!.Steps.Count);
        Assert.Equal("step one", plan.Steps[0].Intent);

        var events = await _store.GetEventsAsync(workflow.Id);
        Assert.Contains(events, e => e.EventType == WorkflowEventTypes.WorkflowCreated);
        Assert.Contains(events, e => e.EventType == WorkflowEventTypes.PlanRevised);
        Assert.DoesNotContain(events, e => e.EventType == WorkflowEventTypes.RunStarted);
    }

    [Fact]
    public async Task SavePlanAsync_BeforeAnyRun_PersistsEditedSteps()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner());
        var workflow = await service.GenerateAsync("build something");

        var edited = await service.SavePlanAsync(workflow.Id,
        [
            new RuntimeStep(0, "edited step one", "edited outcome", RuntimeModelTier.High),
        ]);

        var plan = WorkflowPlanJson.TryDeserialize(edited.PlanJson, edited.Goal);
        Assert.NotNull(plan);
        Assert.Single(plan!.Steps);
        Assert.Equal("edited step one", plan.Steps[0].Intent);
        Assert.Equal(RuntimeModelTier.High, plan.Steps[0].ModelTier);
        Assert.Equal(WorkflowStatus.AwaitingHuman, edited.Status);
    }

    [Fact]
    public async Task SavePlanAsync_AfterRunStarted_Throws()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner());
        var workflow = await service.GenerateAsync("build something");
        await _store.AppendEventAsync(workflow.Id, WorkflowEventTypes.RunStarted, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SavePlanAsync(workflow.Id, [new RuntimeStep(0, "too late", "n/a", RuntimeModelTier.Standard)]));
    }

    [Fact]
    public async Task SavePlanAsync_EmptyStepList_Throws()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner());
        var workflow = await service.GenerateAsync("build something");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SavePlanAsync(workflow.Id, []));
    }
}
