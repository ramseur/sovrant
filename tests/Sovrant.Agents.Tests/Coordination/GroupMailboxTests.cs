using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Coordination;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Coordination;

public sealed class GroupMailboxTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ICoordinationEventStore _store;
    private readonly GroupMailbox _mailbox;

    public GroupMailboxTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_mailbox_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteCoordinationEventStore(_provider);
        _mailbox = new GroupMailbox("group-a", "ws-1", null, _store);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task PostAsync_EnqueuesEvent()
    {
        var evt = new CoordinationEvent("group-a", "group-b", CoordinationEventType.Update, "hello", "ws-1");

        var id = await _mailbox.PostAsync(evt);

        Assert.True(id > 0);
        var pending = await _store.GetPendingAsync("group-b");
        Assert.Single(pending);
    }

    [Fact]
    public async Task DrainPendingAsync_ReturnsEventsAndMarksDelivered()
    {
        // Enqueue an event targeted at group-a
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-b", "group-a", "Blocker", "blocked!", "pending", "ws-1", null, "", null, null));

        var events = await _mailbox.DrainPendingAsync();

        Assert.Single(events);
        Assert.Equal(CoordinationEventType.Blocker, events[0].EventType);
        Assert.Equal("blocked!", events[0].Payload);

        // Should be marked delivered
        var stillPending = await _store.GetPendingAsync("group-a");
        Assert.Empty(stillPending);
    }

    [Fact]
    public async Task DrainPendingAsync_NoPending_ReturnsEmpty()
    {
        var events = await _mailbox.DrainPendingAsync();
        Assert.Empty(events);
    }

    [Fact]
    public async Task PeekPendingCountAsync_ReturnsCount()
    {
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-b", "group-a", "Update", "msg1", "pending", "ws-1", null, "", null, null));
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-c", "group-a", "Update", "msg2", "pending", "ws-1", null, "", null, null));

        var count = await _mailbox.PeekPendingCountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AcknowledgeAsync_UpdatesEventStatus()
    {
        var id = await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-b", "group-a", "Update", "msg", "pending", "ws-1", null, "", null, null));

        await _store.MarkDeliveredAsync(id);
        await _mailbox.AcknowledgeAsync(id);

        var events = await _store.ListByGroupAsync("group-a");
        var single = Assert.Single(events);
        Assert.Equal("acknowledged", single.Status);
    }

    [Fact]
    public async Task DrainPendingAsync_IgnoresInvalidEventTypes()
    {
        // Enqueue event with invalid event type
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-b", "group-a", "InvalidType", "msg", "pending", "ws-1", null, "", null, null));

        var events = await _mailbox.DrainPendingAsync();

        // Invalid event type should be skipped
        Assert.Empty(events);
    }
}
