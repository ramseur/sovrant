using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Engine;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Workflows;

public sealed class WorkflowPlanningServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteWorkflowStore _store;
    private readonly InMemorySessionStore _sessionStore = new();
    private readonly WorkflowSessionNotifier _notifier;

    public WorkflowPlanningServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_workflow_planning_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteWorkflowStore(_provider);
        _notifier = new WorkflowSessionNotifier(_sessionStore, NullLogger<WorkflowSessionNotifier>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file still held (SQLite pool, indexer, antivirus) */ }
    }

    private sealed class InMemorySessionStore : ISessionStore
    {
        public List<(string SessionId, SessionEntry Entry)> Appended { get; } = [];

        public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
        {
            Appended.Add((sessionId, entry));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionEntry>>(
                Appended.Where(a => a.SessionId == sessionId).Select(a => a.Entry).ToList());
        public Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult(true);
        public Task<int> DeleteAllAsync(CancellationToken ct = default)
            => Task.FromResult(0);
        public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task SetTitleAsync(string sessionId, string title, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<string?> GetTitleAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<SessionListItem>> ListWithTitlesAsync(string? ownerUserId = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionListItem>>([]);
        public Task<IReadOnlyList<SessionListItem>> SearchAsync(string query, string? ownerUserId = null, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SessionListItem>>([]);
        public Task<IReadOnlyList<string>?> GetMcpConnectionsAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>?>(null);
        public Task SetMcpConnectionsAsync(string sessionId, IReadOnlyList<string>? servers, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task UpdatePrivacyAsync(string sessionId, string ownerUserId, bool isPrivate, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<bool?> GetIsPrivateAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<bool?>(null);
        public Task SetAgentNameAsync(string sessionId, string agentName, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task<string?> GetAgentNameAsync(string sessionId, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
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
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);

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
    public async Task GenerateAsync_SeedsLinkedSessionWithGoalImmediately()
    {
        // Without this, the chat link on a still-Planning workflow shows
        // nothing at all -- neither planning nor step execution write to
        // the workflow's own session until well after creation.
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);

        var workflow = await service.GenerateAsync("build something");

        var posted = Assert.Single(_sessionStore.Appended);
        Assert.Equal(workflow.SessionId, posted.SessionId);
        Assert.Equal("user", posted.Entry.Role);
        Assert.Equal("build something", posted.Entry.Content);
    }

    [Fact]
    public async Task GenerateAsync_ExistingChatSession_DoesNotDuplicateTheGoalAsANewMessage()
    {
        // Mirrors the Workflow tool: a model can pass its own current chat
        // session id when spawning a workflow mid-conversation. That session
        // already has the user's real message -- seeding a synthetic
        // duplicate would corrupt a real conversation.
        var existingSessionId = "chat-already-in-progress";
        await _sessionStore.AppendAsync(existingSessionId, new SessionEntry(
            Id: "real-1", Timestamp: DateTimeOffset.UtcNow, Role: "user", Content: "let's build something"));

        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);
        await service.GenerateAsync("build something", sessionId: existingSessionId);

        Assert.Single(_sessionStore.Appended); // still just the original real message
    }

    [Fact]
    public async Task SavePlanAsync_BeforeAnyRun_PersistsEditedSteps()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);
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
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);
        var workflow = await service.GenerateAsync("build something");
        await _store.AppendEventAsync(workflow.Id, WorkflowEventTypes.RunStarted, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SavePlanAsync(workflow.Id, [new RuntimeStep(0, "too late", "n/a", RuntimeModelTier.Standard)]));
    }

    [Fact]
    public async Task SavePlanAsync_EmptyStepList_Throws()
    {
        var service = new WorkflowPlanningService(_store, new TwoStepPlanner(), _notifier);
        var workflow = await service.GenerateAsync("build something");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SavePlanAsync(workflow.Id, []));
    }
}
