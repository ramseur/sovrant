using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class WorkflowExportServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;
    private readonly WorkflowExportService _exporter;

    public WorkflowExportServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_export_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
        _exporter = new WorkflowExportService(_store);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task ExportMarkdown_ContainsGoalAndTimeline()
    {
        var m = await _store.CreateAsync("fix the auth bug", ownerUserId: "alice");
        await _store.AppendEventAsync(m.Id, WorkflowEventTypes.RunStarted,
            """{"runtime_run_id":"run-1"}""");
        await _store.AppendEventAsync(m.Id, WorkflowEventTypes.RunCompleted,
            """{"terminal_state":"Completed"}""");
        await _store.AppendEventAsync(m.Id, WorkflowEventTypes.Completed, "{}");
        await _store.UpdateStateAsync(m.Id, WorkflowStatus.Completed,
            completedAt: DateTimeOffset.UtcNow);

        var md = await _exporter.ExportMarkdownAsync(m.Id);

        Assert.Contains("fix the auth bug", md, StringComparison.Ordinal);
        Assert.Contains("## Timeline", md, StringComparison.Ordinal);
        Assert.Contains("mission_created", md, StringComparison.Ordinal);
        Assert.Contains("run_started", md, StringComparison.Ordinal);
        Assert.Contains("## Summary", md, StringComparison.Ordinal);
        Assert.Contains("Engine runs:** 1", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportJson_ContainsWorkflowAndEvents()
    {
        var m = await _store.CreateAsync("deploy the hotfix");
        await _store.AppendEventAsync(m.Id, WorkflowEventTypes.PlanRevised,
            """{"plan_id":"p1"}""");

        var json = await _exporter.ExportJsonAsync(m.Id);

        Assert.Contains("deploy the hotfix", json, StringComparison.Ordinal);
        Assert.Contains("mission_created", json, StringComparison.Ordinal);
        Assert.Contains("plan_revised", json, StringComparison.Ordinal);
        Assert.Contains("\"total_events\": 2", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_UnknownWorkflow_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _exporter.ExportMarkdownAsync("workflow-nope"));
    }
}
