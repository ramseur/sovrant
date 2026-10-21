using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server.Tests;

/// <summary>
/// Tests for <see cref="WorkflowSchedulerService"/> — the background loop
/// that advances Planning/Running workflows without a caller explicitly
/// asking. Uses the real <see cref="IWorkflowStore"/> wired into
/// <see cref="SovrantWebAppFactory"/> (so workflow rows are real, durable
/// state) but a fake <see cref="IWorkflowExecutor"/> so no LLM call happens.
/// </summary>
public sealed class WorkflowSchedulerServiceTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;

    public WorkflowSchedulerServiceTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
    }

    private sealed class FakeWorkflowExecutor : IWorkflowExecutor
    {
        public HashSet<string> CalledIds { get; } = new(StringComparer.Ordinal);

        /// <summary>Workflow ID → the ambient user it ran as (Phase 145 stopgap).</summary>
        public System.Collections.Concurrent.ConcurrentDictionary<string, string?> RanAs { get; } = new(StringComparer.Ordinal);

        public Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
        {
            CalledIds.Add(workflowId);
            RanAs[workflowId] = Sovrant.Runtime.Auth.AmbientPrincipal.Current?.UserId;
            return Task.FromResult(new Workflow(
                workflowId, "goal", WorkflowStatus.Completed, "{}",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public void Constructor_ResolvesPollIntervalAndConcurrency_FromEnvVars()
    {
        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", "42");
        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", "7");
        try
        {
            var store = _factory.Services.GetRequiredService<IWorkflowStore>();
            var settings = _factory.Services.GetRequiredService<IWorkspaceSettingsStore>();
            var service = new WorkflowSchedulerService(
                store, new FakeWorkflowExecutor(), settings, NullLogger<WorkflowSchedulerService>.Instance);

            Assert.Equal(TimeSpan.FromSeconds(42), service.PollInterval);
            Assert.Equal(7, service.MaxConcurrent);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", null);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", null);
        }
    }

    [Fact]
    public async Task Workflows_Run_As_Their_Owner()
    {
        // Tool policies (Phase 145 stopgap) need to know whose workflow is running when no request exists.
        var store = _factory.Services.GetRequiredService<IWorkflowStore>();
        var settings = _factory.Services.GetRequiredService<IWorkspaceSettingsStore>();
        var workflow = await store.CreateAsync("owner test goal", ownerUserId: "owner-test@example.com");
        var fake = new FakeWorkflowExecutor();

        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", "1");
        WorkflowSchedulerService? service = null;
        try
        {
            service = new WorkflowSchedulerService(store, fake, settings, NullLogger<WorkflowSchedulerService>.Instance);
            await service.StartAsync(CancellationToken.None);
            for (var i = 0; i < 50 && !fake.RanAs.ContainsKey(workflow.Id); i++)
                await Task.Delay(100);
        }
        finally
        {
            if (service is not null)
                await service.StopAsync(CancellationToken.None);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", null);
        }

        Assert.True(fake.RanAs.TryGetValue(workflow.Id, out var ranAs), "workflow was not advanced");
        Assert.Equal("owner-test@example.com", ranAs);
        Assert.Null(Sovrant.Runtime.Auth.AmbientPrincipal.Current); // nothing leaks into the test's own flow
    }

    [Fact]
    public async Task PlanningWorkflow_IsAdvanced_WithoutManualRun()
    {
        var store = _factory.Services.GetRequiredService<IWorkflowStore>();
        var settings = _factory.Services.GetRequiredService<IWorkspaceSettingsStore>();
        var workflow = await store.CreateAsync("scheduler test goal", ownerUserId: "scheduler-test-owner");
        var fake = new FakeWorkflowExecutor();

        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", "1");
        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", "5");
        WorkflowSchedulerService? service = null;
        try
        {
            service = new WorkflowSchedulerService(store, fake, settings, NullLogger<WorkflowSchedulerService>.Instance);
            await service.StartAsync(CancellationToken.None);

            // One poll tick at 1s, plus slack for the tick itself to run.
            await Task.Delay(TimeSpan.FromSeconds(2.5));
        }
        finally
        {
            if (service is not null)
                await service.StopAsync(CancellationToken.None);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", null);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", null);
        }

        Assert.Contains(workflow.Id, fake.CalledIds);
    }

    [Fact]
    public async Task AwaitingHumanWorkflow_IsNotAdvanced()
    {
        var store = _factory.Services.GetRequiredService<IWorkflowStore>();
        var settings = _factory.Services.GetRequiredService<IWorkspaceSettingsStore>();
        var workflow = await store.CreateAsync("awaiting-human test goal", ownerUserId: "scheduler-test-owner");
        await store.UpdateStateAsync(workflow.Id, WorkflowStatus.AwaitingHuman);
        var fake = new FakeWorkflowExecutor();

        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", "1");
        Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", "5");
        WorkflowSchedulerService? service = null;
        try
        {
            service = new WorkflowSchedulerService(store, fake, settings, NullLogger<WorkflowSchedulerService>.Instance);
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(2.5));
        }
        finally
        {
            if (service is not null)
                await service.StopAsync(CancellationToken.None);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_POLL_SECONDS", null);
            Environment.SetEnvironmentVariable("SOVRANT_WORKFLOW_MAX_CONCURRENT", null);
        }

        Assert.DoesNotContain(workflow.Id, fake.CalledIds);
    }
}
