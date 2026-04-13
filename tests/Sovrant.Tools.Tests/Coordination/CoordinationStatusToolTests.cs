using System.Text.Json;
using Sovrant.Runtime.Storage;
using Sovrant.Tools.Coordination;

namespace Sovrant.Tools.Tests.Coordination;

public sealed class CoordinationStatusToolTests
{
    [Fact]
    public void Definition_Name_IsCoordinationStatus()
    {
        var tool = new CoordinationStatusTool(new FakeCoordinationEventStore());
        Assert.Equal("CoordinationStatus", tool.Definition.Name);
    }

    [Fact]
    public async Task Execute_NoGroupId_ReturnsHelpMessage()
    {
        var tool = new CoordinationStatusTool(new FakeCoordinationEventStore());
        var input = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(input);
        Assert.Contains("No group_id provided", result);
    }

    [Fact]
    public async Task Execute_WithGroupId_ReturnsStatusAndEvents()
    {
        var store = new FakeCoordinationEventStore();
        await store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-a", "group-b", "Update", "important update", "pending", "ws-1", null, "", null, null));

        var tool = new CoordinationStatusTool(store);
        var input = JsonDocument.Parse("""{"group_id": "group-b"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("group-b", result);
        Assert.Contains("Pending messages: 1", result);
        Assert.Contains("Update", result);
        Assert.Contains("important update", result);
    }

    /// <summary>Simple in-memory fake for testing the tool without SQLite.</summary>
    private sealed class FakeCoordinationEventStore : ICoordinationEventStore
    {
        private readonly List<CoordinationEventRecord> _events = [];
        private long _nextId = 1;

        public Task<long> EnqueueAsync(CoordinationEventRecord record, CancellationToken ct = default)
        {
            var id = _nextId++;
            _events.Add(record with { Id = id, CreatedAt = DateTime.UtcNow.ToString("o") });
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<CoordinationEventRecord>> GetPendingAsync(string targetGroupId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CoordinationEventRecord>>(
                _events.Where(e => e.TargetGroupId == targetGroupId && e.Status == "pending").ToList());

        public Task MarkDeliveredAsync(long eventId, CancellationToken ct = default)
        {
            var idx = _events.FindIndex(e => e.Id == eventId);
            if (idx >= 0) _events[idx] = _events[idx] with { Status = "delivered" };
            return Task.CompletedTask;
        }

        public Task MarkAcknowledgedAsync(long eventId, CancellationToken ct = default)
        {
            var idx = _events.FindIndex(e => e.Id == eventId);
            if (idx >= 0) _events[idx] = _events[idx] with { Status = "acknowledged" };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CoordinationEventRecord>> ListByGroupAsync(string groupId, int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CoordinationEventRecord>>(
                _events.Where(e => e.SourceGroupId == groupId || e.TargetGroupId == groupId)
                    .Take(limit).ToList());

        public Task<int> GetPendingCountAsync(string targetGroupId, CancellationToken ct = default) =>
            Task.FromResult(_events.Count(e => e.TargetGroupId == targetGroupId && e.Status == "pending"));
    }
}
