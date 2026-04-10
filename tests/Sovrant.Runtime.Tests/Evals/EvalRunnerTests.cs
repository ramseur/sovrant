using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class EvalRunnerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteEvalResultStore _resultStore;
    private readonly EvalRunner _runner;

    public EvalRunnerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_eval_runner_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _resultStore = new SqliteEvalResultStore((ISqliteConnectionFactory)_provider);
        var router = new StubRouter();
        _runner = new EvalRunner(router, _resultStore, NullLogger<EvalRunner>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task RunAsync_SinglePassingEval()
    {
        var suite = new EvalSuite
        {
            Name = "simple",
            Evals = [
                new EvalDefinition
                {
                    Name = "always-pass",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "pass" },
                },
            ],
        };

        var provider = new FixedOutputProvider("this will pass");
        var report = await _runner.RunAsync(suite, provider);

        Assert.Equal("simple", report.SuiteName);
        Assert.Single(report.Results);
        Assert.True(report.Results[0].Passed);
        Assert.Equal(1.0, report.PassRate);
    }

    [Fact]
    public async Task RunAsync_SingleFailingEval()
    {
        var suite = new EvalSuite
        {
            Name = "fail",
            Evals = [
                new EvalDefinition
                {
                    Name = "always-fail",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "impossible_match_xyz" },
                },
            ],
        };

        var provider = new FixedOutputProvider("does not match");
        var report = await _runner.RunAsync(suite, provider);

        Assert.Single(report.Results);
        Assert.False(report.Results[0].Passed);
        Assert.Equal(0.0, report.PassRate);
    }

    [Fact]
    public async Task RunAsync_HumanGrader_MarkedAsSkipped()
    {
        var suite = new EvalSuite
        {
            Name = "human",
            Evals = [
                new EvalDefinition
                {
                    Name = "human-review",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Human },
                },
            ],
        };

        var report = await _runner.RunAsync(suite);

        Assert.Single(report.Results);
        Assert.True(report.Results[0].Skipped);
        Assert.Equal(1, report.TotalSkipped);
    }

    [Fact]
    public async Task RunAsync_MultipleAttempts_PassAtK()
    {
        var suite = new EvalSuite
        {
            Name = "multi-attempt",
            Evals = [
                new EvalDefinition
                {
                    Name = "needs-retry",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "attempt_2" },
                    Attempts = 3,
                    PassThreshold = 1,
                },
            ],
        };

        // Returns different output per attempt
        var provider = new SequentialOutputProvider(["attempt_1", "attempt_2", "attempt_3"]);
        var report = await _runner.RunAsync(suite, provider);

        Assert.Single(report.Results);
        Assert.True(report.Results[0].Passed);
        Assert.False(report.Results[0].PassAt1); // First attempt doesn't match
        Assert.True(report.Results[0].PassAtK);
    }

    [Fact]
    public async Task RunAsync_EarlyExit_WhenThresholdMet()
    {
        var suite = new EvalSuite
        {
            Name = "early-exit",
            Evals = [
                new EvalDefinition
                {
                    Name = "quick-pass",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "pass" },
                    Attempts = 5,
                    PassThreshold = 1,
                },
            ],
        };

        var provider = new FixedOutputProvider("this will pass");
        var report = await _runner.RunAsync(suite, provider);

        // Should stop after first successful attempt
        Assert.Single(report.Results[0].Attempts);
    }

    [Fact]
    public async Task RunAsync_MixedSuite()
    {
        var suite = new EvalSuite
        {
            Name = "mixed",
            Evals = [
                new EvalDefinition
                {
                    Name = "pass-eval",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "good" },
                },
                new EvalDefinition
                {
                    Name = "fail-eval",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "impossible" },
                },
                new EvalDefinition
                {
                    Name = "skip-eval",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Human },
                },
            ],
        };

        var provider = new FixedOutputProvider("this is good output");
        var report = await _runner.RunAsync(suite, provider);

        Assert.Equal(3, report.Results.Count);
        Assert.Equal(1, report.TotalPassed);
        Assert.Equal(1, report.TotalFailed);
        Assert.Equal(1, report.TotalSkipped);
        Assert.Equal(0.5, report.PassRate); // 1 pass out of 2 graded
    }

    [Fact]
    public async Task RunAsync_PersistsResults()
    {
        var suite = new EvalSuite
        {
            Name = "persist-test",
            Evals = [
                new EvalDefinition
                {
                    Name = "persisted",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "ok" },
                },
            ],
        };

        await _runner.RunAsync(suite, new FixedOutputProvider("ok"));

        // Verify persisted to SQLite
        var history = _resultStore.LoadHistory("persist-test");
        Assert.Single(history);
        Assert.Equal("persist-test", history[0].SuiteName);
    }

    [Fact]
    public async Task RunAsync_Duration_IsTracked()
    {
        var suite = new EvalSuite
        {
            Name = "timed",
            Evals = [
                new EvalDefinition
                {
                    Name = "timed-eval",
                    Prompt = "test",
                    Grader = new GraderConfig { Type = GraderType.Code, Pattern = "x" },
                },
            ],
        };

        var report = await _runner.RunAsync(suite, new FixedOutputProvider("x"));

        Assert.True(report.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_NullSuite_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunAsync(null!, new FixedOutputProvider("x")));
    }

    [Fact]
    public async Task RunAsync_NullProvider_Throws()
    {
        var suite = new EvalSuite { Name = "test", Evals = [] };
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.RunAsync(suite, null!));
    }

    // ── Test helpers ───────────────────────────────────────────────────────────

    private sealed class FixedOutputProvider : IEvalOutputProvider
    {
        private readonly string _output;
        public FixedOutputProvider(string output) => _output = output;
        public Task<string> GetOutputAsync(EvalDefinition eval, int attemptIndex, CancellationToken ct) =>
            Task.FromResult(_output);
    }

    private sealed class SequentialOutputProvider : IEvalOutputProvider
    {
        private readonly IReadOnlyList<string> _outputs;
        public SequentialOutputProvider(IReadOnlyList<string> outputs) => _outputs = outputs;
        public Task<string> GetOutputAsync(EvalDefinition eval, int attemptIndex, CancellationToken ct) =>
            Task.FromResult(attemptIndex < _outputs.Count ? _outputs[attemptIndex] : string.Empty);
    }

    private sealed class StubRouter : ISmartRouter
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
            throw new InvalidOperationException("StubRouter: no provider configured");
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
            Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            throw new InvalidOperationException("StubRouter: no provider configured");
        public bool IntentRoutingEnabled { get; set; }
    }
}
