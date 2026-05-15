using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Coordination;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Tests.Coordination;

public sealed class PMCoordinatorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ICoordinationEventStore _store;
    private readonly PMCoordinator _coordinator;

    public PMCoordinatorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_pmcoord_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteCoordinationEventStore(_provider);
        _coordinator = new PMCoordinator(NullLogger<PMCoordinator>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private GroupMailbox CreateMailbox(string groupId) =>
        new(groupId, "ws-1", null, _store);

    [Fact]
    public void RegisterGroup_ThenUnregister_DoesNotThrow()
    {
        var pm = new FakePMAgent("group-a");
        var mailbox = CreateMailbox("group-a");

        _coordinator.RegisterGroup("group-a", pm, mailbox);
        _coordinator.UnregisterGroup("group-a");
    }

    [Fact]
    public async Task RouteEvent_DirectTarget_DeliversToMailbox()
    {
        var pm = new FakePMAgent("group-b");
        var mailbox = CreateMailbox("group-b");
        _coordinator.RegisterGroup("group-b", pm, mailbox);

        var evt = new CoordinationEvent("group-a", "group-b", CoordinationEventType.Update, "test", "ws-1");
        await _coordinator.RouteEventAsync(evt);

        var pending = await _store.GetPendingAsync("group-b");
        Assert.Single(pending);
        Assert.Equal("test", pending[0].Payload);
    }

    [Fact]
    public async Task RouteEvent_SelfSend_Rejected()
    {
        var pm = new FakePMAgent("group-a");
        var mailbox = CreateMailbox("group-a");
        _coordinator.RegisterGroup("group-a", pm, mailbox);

        var evt = new CoordinationEvent("group-a", "group-a", CoordinationEventType.Update, "self", "ws-1");
        await _coordinator.RouteEventAsync(evt);

        var pending = await _store.GetPendingAsync("group-a");
        Assert.Empty(pending);
    }

    [Fact]
    public async Task RouteEvent_UnknownTarget_Silently()
    {
        var evt = new CoordinationEvent("group-a", "unknown-group", CoordinationEventType.Request, "hello", "ws-1");
        // Should not throw
        await _coordinator.RouteEventAsync(evt);
    }

    [Fact]
    public async Task RouteEvent_Broadcast_DeliversToAllExceptSource()
    {
        var pmB = new FakePMAgent("group-b");
        var pmC = new FakePMAgent("group-c");
        var pmA = new FakePMAgent("group-a");
        _coordinator.RegisterGroup("group-a", pmA, CreateMailbox("group-a"));
        _coordinator.RegisterGroup("group-b", pmB, CreateMailbox("group-b"));
        _coordinator.RegisterGroup("group-c", pmC, CreateMailbox("group-c"));

        var evt = new CoordinationEvent("group-a", "*", CoordinationEventType.Update, "broadcast", "ws-1");
        await _coordinator.RouteEventAsync(evt);

        var pendingB = await _store.GetPendingAsync("group-b");
        var pendingC = await _store.GetPendingAsync("group-c");
        var pendingA = await _store.GetPendingAsync("group-a");

        Assert.Single(pendingB);
        Assert.Single(pendingC);
        Assert.Empty(pendingA);
    }

    [Fact]
    public async Task GetPendingCount_ReturnsCorrectCount()
    {
        var pm = new FakePMAgent("group-b");
        var mailbox = CreateMailbox("group-b");
        _coordinator.RegisterGroup("group-b", pm, mailbox);

        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-a", "group-b", "Update", "msg1", "pending", "ws-1", null, "", null, null));
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-a", "group-b", "Update", "msg2", "pending", "ws-1", null, "", null, null));

        var count = await _coordinator.GetPendingCountAsync("group-b");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetPendingCount_UnregisteredGroup_ReturnsZero()
    {
        var count = await _coordinator.GetPendingCountAsync("nonexistent");
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BroadcastFromGroup_DrainsPendingAndCallsPM()
    {
        var pm = new FakePMAgent("group-a");
        var mailbox = CreateMailbox("group-a");
        _coordinator.RegisterGroup("group-a", pm, mailbox);

        // Put an event in group-a's inbox
        await _store.EnqueueAsync(new CoordinationEventRecord(
            0, "group-b", "group-a", "Update", "info", "pending", "ws-1", null, "", null, null));

        await _coordinator.BroadcastFromGroupAsync("group-a");

        Assert.True(pm.ReceivedEvents.Count > 0);
        Assert.True(pm.BroadcastCalled);
    }

    /// <summary>Minimal fake PM agent for testing coordinator routing.</summary>
    private sealed class FakePMAgent(string groupId) : IPMAgent
    {
        public string GroupId => groupId;
        public List<CoordinationEvent> ReceivedEvents { get; } = [];
        public bool BroadcastCalled { get; private set; }

        public Task<IReadOnlyList<CoordinationEvent>> OnGroupUpdateAsync(
            string agentName, string updateSummary, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CoordinationEvent>>([]);

        public Task ReceiveCoordinationAsync(IReadOnlyList<CoordinationEvent> events, CancellationToken ct = default)
        {
            ReceivedEvents.AddRange(events);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CoordinationEvent>> DecideBroadcastAsync(CancellationToken ct = default)
        {
            BroadcastCalled = true;
            return Task.FromResult<IReadOnlyList<CoordinationEvent>>([]);
        }
    }
}
