using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteCoordinationEventStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ICoordinationEventStore _store;

    public SqliteCoordinationEventStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_coord_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteCoordinationEventStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private static CoordinationEventRecord MakeRecord(
        string sourceGroupId = "group-a",
        string targetGroupId = "group-b",
        string eventType = "Update",
        string payload = "test payload",
        string workspaceId = "ws-1",
        string? projectId = null) =>
        new(
            Id: 0,
            SourceGroupId: sourceGroupId,
            TargetGroupId: targetGroupId,
            EventType: eventType,
            Payload: payload,
            Status: "pending",
            WorkspaceId: workspaceId,
            ProjectId: projectId,
            CreatedAt: string.Empty,
            DeliveredAt: null,
            AcknowledgedAt: null);

    [Fact]
    public async Task EnqueueAndGetPending_RoundTrips()
    {
        var id = await _store.EnqueueAsync(MakeRecord());

        Assert.True(id > 0);

        var pending = await _store.GetPendingAsync("group-b");
        var single = Assert.Single(pending);
        Assert.Equal("group-a", single.SourceGroupId);
        Assert.Equal("group-b", single.TargetGroupId);
        Assert.Equal("Update", single.EventType);
        Assert.Equal("test payload", single.Payload);
        Assert.Equal("pending", single.Status);
        Assert.Equal("ws-1", single.WorkspaceId);
    }

    [Fact]
    public async Task GetPending_UnknownTarget_ReturnsEmpty()
    {
        await _store.EnqueueAsync(MakeRecord());
        var pending = await _store.GetPendingAsync("nonexistent");
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkDelivered_RemovesFromPending()
    {
        var id = await _store.EnqueueAsync(MakeRecord());

        await _store.MarkDeliveredAsync(id);

        var pending = await _store.GetPendingAsync("group-b");
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkAcknowledged_UpdatesStatus()
    {
        var id = await _store.EnqueueAsync(MakeRecord());
        await _store.MarkDeliveredAsync(id);
        await _store.MarkAcknowledgedAsync(id);

        var events = await _store.ListByGroupAsync("group-b");
        var single = Assert.Single(events);
        Assert.Equal("acknowledged", single.Status);
        Assert.NotNull(single.AcknowledgedAt);
    }

    [Fact]
    public async Task ListByGroup_IncludesSourceAndTargetEvents()
    {
        await _store.EnqueueAsync(MakeRecord(sourceGroupId: "group-a", targetGroupId: "group-b"));
        await _store.EnqueueAsync(MakeRecord(sourceGroupId: "group-b", targetGroupId: "group-c"));
        await _store.EnqueueAsync(MakeRecord(sourceGroupId: "group-c", targetGroupId: "group-d"));

        var events = await _store.ListByGroupAsync("group-b");
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task ListByGroup_RespectsLimit()
    {
        for (var i = 0; i < 5; i++)
            await _store.EnqueueAsync(MakeRecord());

        var events = await _store.ListByGroupAsync("group-a", limit: 2);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public async Task GetPendingCount_ReturnsCorrectCount()
    {
        await _store.EnqueueAsync(MakeRecord());
        await _store.EnqueueAsync(MakeRecord());
        var id = await _store.EnqueueAsync(MakeRecord());
        await _store.MarkDeliveredAsync(id);

        var count = await _store.GetPendingCountAsync("group-b");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Enqueue_WithProjectId_PersistsCorrectly()
    {
        await _store.EnqueueAsync(MakeRecord(projectId: "proj-1"));

        var pending = await _store.GetPendingAsync("group-b");
        var single = Assert.Single(pending);
        Assert.Equal("proj-1", single.ProjectId);
    }
}
