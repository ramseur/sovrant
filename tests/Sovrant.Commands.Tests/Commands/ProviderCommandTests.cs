using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Commands.Commands;

namespace Sovrant.Commands.Tests.Commands;

public sealed class ProviderCommandTests
{
    // ── Fake router ────────────────────────────────────────────────────────────

    private sealed class FakeRouter : ISmartRouter
    {
        public string? PinnedName { get; private set; }
        public bool ThrowOnPin { get; init; }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
            Task.CompletedTask;

        public IReadOnlyList<ProviderStatus> GetStatus() =>
        [
            new ProviderStatus("alpha", true, 42.0, 0.002, 5, 0, "0.0%", "0.5"),
            new ProviderStatus("beta", false, 0, 0.001, 0, 0, "0.0%", "N/A"),
        ];

        public Task PinProviderAsync(string? providerName, CancellationToken ct = default)
        {
            if (ThrowOnPin)
                throw new InvalidOperationException($"Provider '{providerName}' is not configured.");
            PinnedName = providerName;
            return Task.CompletedTask;
        }

        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            RouteAsync(req, ct).ContinueWith(t => new RoutingDecision(t.Result, null, null, null), ct);
        public bool IntentRoutingEnabled { get; set; }
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoArgs_ReturnsStatusTable()
    {
        var cmd = new ProviderCommand(new FakeRouter());

        var result = await cmd.ExecuteAsync(string.Empty);

        Assert.NotNull(result.Output);
        Assert.Contains("alpha", result.Output, StringComparison.Ordinal);
        Assert.Contains("yes", result.Output, StringComparison.Ordinal);
        Assert.Contains("beta", result.Output, StringComparison.Ordinal);
        Assert.Contains("NO", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithName_PinsProviderAndReturnsConfirmation()
    {
        var router = new FakeRouter();
        var cmd = new ProviderCommand(router);

        var result = await cmd.ExecuteAsync("alpha");

        Assert.Equal("alpha", router.PinnedName);
        Assert.NotNull(result.Output);
        Assert.Contains("alpha", result.Output, StringComparison.Ordinal);
        Assert.Contains("Pinned", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithUnknownName_ReturnsErrorMessage()
    {
        var router = new FakeRouter { ThrowOnPin = true };
        var cmd = new ProviderCommand(router);

        var result = await cmd.ExecuteAsync("nonexistent");

        Assert.NotNull(result.Output);
        Assert.Contains("nonexistent", result.Output, StringComparison.Ordinal);
        Assert.False(result.ShouldExit);
    }

    [Fact]
    public async Task Name_IsProvider()
    {
        var cmd = new ProviderCommand(new FakeRouter());
        Assert.Equal("provider", cmd.Name);
        await Task.CompletedTask;
    }
}
