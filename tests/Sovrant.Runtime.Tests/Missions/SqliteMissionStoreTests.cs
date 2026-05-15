using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Missions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Missions;

public sealed class SqliteMissionStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMissionStore _store;

    public SqliteMissionStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_missions_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMissionStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task Create_PersistsRowAndWritesCreatedEvent()
    {
        var mission = await _store.CreateAsync("ship the bugfix", ownerUserId: "alice");

        Assert.Equal(MissionStatus.Planning, mission.Status);
        Assert.Equal("ship the bugfix", mission.Goal);
        Assert.Equal("alice", mission.OwnerUserId);

        var fetched = await _store.GetAsync(mission.Id);
        Assert.NotNull(fetched);
        Assert.Equal(mission.Id, fetched!.Id);

        var events = await _store.GetEventsAsync(mission.Id);
        Assert.Single(events);
        Assert.Equal(MissionEventTypes.MissionCreated, events[0].EventType);
    }

    [Fact]
    public async Task Get_NonExistent_ReturnsNull()
    {
        var missing = await _store.GetAsync("does-not-exist");
        Assert.Null(missing);
    }

    [Fact]
    public async Task List_FiltersByOwnerAndStatus()
    {
        var m1 = await _store.CreateAsync("goal one", ownerUserId: "alice");
        var m2 = await _store.CreateAsync("goal two", ownerUserId: "alice");
        var m3 = await _store.CreateAsync("goal three", ownerUserId: "bob");

        await _store.UpdateStateAsync(m2.Id, MissionStatus.Completed,
            completedAt: DateTimeOffset.UtcNow);

        var aliceAll = await _store.ListAsync(ownerUserId: "alice");
        Assert.Equal(2, aliceAll.Count);

        var alicePlanning = await _store.ListAsync(
            ownerUserId: "alice", status: MissionStatus.Planning);
        Assert.Single(alicePlanning);
        Assert.Equal(m1.Id, alicePlanning[0].Id);

        var allCompleted = await _store.ListAsync(status: MissionStatus.Completed);
        Assert.Single(allCompleted);
        Assert.Equal(m2.Id, allCompleted[0].Id);

        var all = await _store.ListAsync();
        Assert.Equal(3, all.Count);
        Assert.DoesNotContain(all, m => m.Id == m3.Id + "-bogus");
    }

    [Fact]
    public async Task UpdateState_PersistsNewStatusAndPlan()
    {
        var m = await _store.CreateAsync("plan me");
        await _store.UpdateStateAsync(
            m.Id, MissionStatus.Running, planJson: """{"v":1}""");

        var fetched = (await _store.GetAsync(m.Id))!;
        Assert.Equal(MissionStatus.Running, fetched.Status);
        Assert.Equal("""{"v":1}""", fetched.PlanJson);
        Assert.Null(fetched.CompletedAt);

        var when = DateTimeOffset.UtcNow;
        await _store.UpdateStateAsync(m.Id, MissionStatus.Completed, completedAt: when);
        fetched = (await _store.GetAsync(m.Id))!;
        Assert.Equal(MissionStatus.Completed, fetched.Status);
        Assert.NotNull(fetched.CompletedAt);
    }

    [Fact]
    public async Task UpdateState_NullPlanJson_PreservesExistingPlan()
    {
        var m = await _store.CreateAsync("keep plan");
        await _store.UpdateStateAsync(m.Id, MissionStatus.Running, planJson: """{"v":1}""");
        await _store.UpdateStateAsync(m.Id, MissionStatus.AwaitingHuman, planJson: null);

        var fetched = (await _store.GetAsync(m.Id))!;
        Assert.Equal(MissionStatus.AwaitingHuman, fetched.Status);
        Assert.Equal("""{"v":1}""", fetched.PlanJson);
    }

    [Fact]
    public async Task AppendEvent_PreservesInsertionOrder()
    {
        var m = await _store.CreateAsync("journal test");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.RunStarted, """{"i":1}""");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.RunCompleted, """{"i":2}""");
        await _store.AppendEventAsync(m.Id, MissionEventTypes.Completed, """{"i":3}""");

        var events = await _store.GetEventsAsync(m.Id);
        Assert.Equal(4, events.Count); // includes mission_created
        Assert.Equal(MissionEventTypes.MissionCreated, events[0].EventType);
        Assert.Equal(MissionEventTypes.RunStarted, events[1].EventType);
        Assert.Equal(MissionEventTypes.RunCompleted, events[2].EventType);
        Assert.Equal(MissionEventTypes.Completed, events[3].EventType);

        // IDs strictly increasing — proves insertion order survives read back.
        for (int i = 1; i < events.Count; i++)
            Assert.True(events[i].Id > events[i - 1].Id);
    }

    [Fact]
    public async Task List_LimitIsRespected()
    {
        for (int i = 0; i < 5; i++)
            await _store.CreateAsync($"goal {i}");

        var limited = await _store.ListAsync(limit: 3);
        Assert.Equal(3, limited.Count);
    }
}
