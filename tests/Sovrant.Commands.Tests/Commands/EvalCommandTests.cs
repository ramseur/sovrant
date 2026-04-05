using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Evals;

namespace Sovrant.Commands.Tests.Commands;

public sealed class EvalCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-eval-cmd-{Guid.NewGuid():N}");
    private readonly string _evalsDir;
    private readonly EvalCommand _command;

    public EvalCommandTests()
    {
        _evalsDir = Path.Combine(_tempDir, ".sovrant", "evals");
        Directory.CreateDirectory(_evalsDir);

        var resultStore = new EvalResultStore(Path.Combine(_tempDir, "results"));
        var router = new StubRouter();
        var runner = new EvalRunner(router, resultStore, NullLogger<EvalRunner>.Instance);
        _command = new EvalCommand(runner, resultStore);
    }

    public void Dispose()
    {
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
    }
}
