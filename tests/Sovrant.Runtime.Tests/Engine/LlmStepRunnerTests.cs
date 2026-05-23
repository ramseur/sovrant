using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Tests.Engine;

public sealed class LlmStepRunnerTests
{
    // ── Fakes ────────────────────────────────────────────────────────────

    private sealed class FakeRuntime : IConversationRuntime
    {
        public string SessionId => "sess-fake";
        public List<RuntimeEvent> NextEvents { get; } = new();
        public string? LastPrompt { get; private set; }
        public int TurnCount { get; private set; }

        public Task InitializeSessionAsync(string? sessionId, string? ownerUserId = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<RuntimeEvent> RunTurnAsync(
            string userMessage,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastPrompt = userMessage;
            TurnCount++;
            foreach (var evt in NextEvents)
            {
                yield return evt;
                await Task.Yield();
            }
        }

        public void Reset() { }
    }

    private sealed class FakePool : IRuntimeSessionPool
    {
        public FakeRuntime Runtime { get; } = new();
        public PooledSession Session { get; }

        public FakePool()
        {
            Session = new PooledSession(Runtime, new SemaphoreSlim(1, 1), new SessionConfig());
        }

        public Task<PooledSession> GetOrCreateAsync(
            string sessionId,
            Sovrant.Api.Routing.ISmartRouter? scopedRouterOverride = null,
            string? ownerUserId = null,
            string? agentSystemPrompt = null,
            string? agentName = null,
            CancellationToken ct = default) => Task.FromResult(Session);

        public void Evict(string sessionId, string? ownerUserId = null) { }
        public int ActiveCount => 1;
        public int EvictExpired(TimeSpan ttl, int maxSessions) => 0;
        public SessionConfig? TryGetConfig(string sessionId, string? ownerUserId = null) => Session.Config;
        public void BeginTurn(string sessionId, string? ownerUserId) { }
        public void EndTurn(string sessionId, string? ownerUserId) { }
        public IReadOnlyList<string> GetActiveTurnSessionIds() => [];
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static RuntimeStep Step(
        int index = 0,
        string intent = "confirm the file exists",
        string expected = "file found",
        RuntimeModelTier tier = RuntimeModelTier.Standard) =>
        new(index, intent, expected, tier);

    private static StepExecutionContext Ctx(
        IReadOnlyList<StepOutcome>? prior = null,
        int attempt = 1) =>
        new(
            RuntimeRunId: "run-test",
            PlanId: "plan-1",
            PlanVersion: 1,
            SessionId: "sess-fake",
            WorkspaceId: "ws-test",
            ProjectId: "proj-test",
            AttemptNumber: attempt,
            PriorOutcomes: prior ?? Array.Empty<StepOutcome>());

    private static LlmStepRunner MakeRunner(FakePool pool) =>
        new(pool, new NaiveContextCompactor(), options: null,
            logger: NullLogger<LlmStepRunner>.Instance);

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_TextChunkAndTurnComplete_ReturnsSucceeded()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TextChunk("checked the file"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("end_turn", 10, 20));

        var runner = MakeRunner(pool);
        var outcome = await runner.RunAsync(Step(), Ctx(), CancellationToken.None);

        Assert.Equal(StepStatus.Succeeded, outcome.Status);
        Assert.True(outcome.MatchedExpectation);
        Assert.Contains("checked the file", outcome.Summary, StringComparison.Ordinal);
        Assert.Equal(1, pool.Runtime.TurnCount);
    }

    [Fact]
    public async Task RunAsync_ToolErrorResult_ReturnsContradicted()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.ToolResult(
            "tu-1", "read_file", "file not found", IsError: true));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("end_turn", 5, 5));

        var runner = MakeRunner(pool);
        var outcome = await runner.RunAsync(Step(), Ctx(), CancellationToken.None);

        Assert.Equal(StepStatus.Contradicted, outcome.Status);
        Assert.False(outcome.MatchedExpectation);
        Assert.NotNull(outcome.ObservationJson);
        Assert.Contains("read_file", outcome.ObservationJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_RuntimeError_ReturnsFailed()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.RuntimeError("provider unreachable"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("error", 0, 0));

        var runner = MakeRunner(pool);
        var outcome = await runner.RunAsync(Step(), Ctx(), CancellationToken.None);

        Assert.Equal(StepStatus.Failed, outcome.Status);
        Assert.Equal("provider unreachable", outcome.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_PermissionDenied_ReturnsFailed()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.PermissionDenied("write_file", "not allowed"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("denied", 0, 0));

        var runner = MakeRunner(pool);
        var outcome = await runner.RunAsync(Step(), Ctx(), CancellationToken.None);

        Assert.Equal(StepStatus.Failed, outcome.Status);
        Assert.Contains("write_file", outcome.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PromptIncludesIntentAndExpectedOutcome()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TextChunk("ok"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("end_turn", 1, 1));

        var runner = MakeRunner(pool);
        await runner.RunAsync(
            Step(intent: "bisect the failure", expected: "commit identified", tier: RuntimeModelTier.High),
            Ctx(),
            CancellationToken.None);

        Assert.NotNull(pool.Runtime.LastPrompt);
        Assert.Contains("bisect the failure", pool.Runtime.LastPrompt!, StringComparison.Ordinal);
        Assert.Contains("commit identified", pool.Runtime.LastPrompt, StringComparison.Ordinal);
        Assert.Contains("High", pool.Runtime.LastPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PromptIncludesPriorOutcomes()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TextChunk("ok"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("end_turn", 1, 1));

        var prior = new[]
        {
            new StepOutcome(0, StepStatus.Succeeded, "listed directory", null, true,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new StepOutcome(1, StepStatus.Succeeded, "found candidate", null, true,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        };

        var runner = MakeRunner(pool);
        await runner.RunAsync(Step(index: 2), Ctx(prior: prior), CancellationToken.None);

        Assert.Contains("listed directory", pool.Runtime.LastPrompt!, StringComparison.Ordinal);
        Assert.Contains("found candidate", pool.Runtime.LastPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SerializesTurnsThroughPoolLock()
    {
        var pool = new FakePool();
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TextChunk("ok"));
        pool.Runtime.NextEvents.Add(new RuntimeEvent.TurnComplete("end_turn", 1, 1));

        var runner = MakeRunner(pool);
        await runner.RunAsync(Step(), Ctx(), CancellationToken.None);

        // Lock should be released after the call returns — CurrentCount back to 1.
        Assert.Equal(1, pool.Session.Lock.CurrentCount);
    }
}
