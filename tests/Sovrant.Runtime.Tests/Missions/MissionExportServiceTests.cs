using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Missions;

public sealed class MissionExportServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _store;
    private readonly MissionExportService _exporter;

    public MissionExportServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_export_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMissionStore(_provider);
        _exporter = new MissionExportService(_store);
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
        await _store.AppendEventAsync(m.Id, MissionEventTypes.RunStarted,
            """{"runtime_run_id":"run-1"}""");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.RunCompleted,
            """{"terminal_state":"Completed"}""");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.Completed, "{}");
        await _store.UpdateStateAsync(m.Id, MissionStatus.Completed,
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
    public async Task ExportJson_ContainsMissionAndEvents()
    {
        var m = await _store.CreateAsync("deploy the hotfix");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.PlanRevised,
            """{"plan_id":"p1"}""");

        var json = await _exporter.ExportJsonAsync(m.Id);

        Assert.Contains("deploy the hotfix", json, StringComparison.Ordinal);
        Assert.Contains("mission_created", json, StringComparison.Ordinal);
        Assert.Contains("plan_revised", json, StringComparison.Ordinal);
        Assert.Contains("\"total_events\": 2", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_UnknownMission_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _exporter.ExportMarkdownAsync("mission-nope"));
    }
}
