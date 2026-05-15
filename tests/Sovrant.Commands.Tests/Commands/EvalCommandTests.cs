using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Storage;

namespace Sovrant.Commands.Tests.Commands;

public sealed class EvalCommandTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-eval-cmd-{Guid.NewGuid():N}");
    private readonly EvalCommand _command;

    public EvalCommandTests()
    {
        var evalsDir = Path.Combine(_tempDir, ".sovrant", "evals");
        Directory.CreateDirectory(evalsDir);

        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_eval_cmd_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();

        var resultStore = new SqliteEvalResultStore((ISqliteConnectionFactory)_provider);
        var router = new StubRouter();
        var runner = new EvalRunner(router, resultStore, NullLogger<EvalRunner>.Instance);
        _command = new EvalCommand(runner, resultStore);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Name_Is_Eval()
    {
        Assert.Equal("eval", _command.Name);
    }

    [Fact]
    public void Aliases_Contains_Evals()
    {
        Assert.Contains("evals", _command.Aliases);
    }

    [Fact]
    public async Task ExecuteAsync_NoSuites_ReturnsHelpfulMessage()
    {
        // Empty evals directory — command searches cwd which has no .sovrant/evals
        var result = await _command.ExecuteAsync("");

        Assert.NotNull(result.Output);
        Assert.Contains("No eval suites found", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_History_NoData_ReturnsUsage()
    {
        var result = await _command.ExecuteAsync("--history");

        Assert.NotNull(result.Output);
        Assert.Contains("Usage", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_History_WithSuiteName_ReturnsNoHistory()
    {
        var result = await _command.ExecuteAsync("--history nonexistent");

        Assert.NotNull(result.Output);
        Assert.Contains("No history found", result.Output);
    }

    private sealed class StubRouter : ISmartRouter
    {
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
            throw new InvalidOperationException("StubRouter");
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
            Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            throw new InvalidOperationException("StubRouter");
        public bool IntentRoutingEnabled { get; set; }
    }
}
