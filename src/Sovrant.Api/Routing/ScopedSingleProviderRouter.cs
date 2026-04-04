using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Routing;

/// <summary>
/// A lightweight <see cref="ISmartRouter"/> that always routes to a single provider.
/// Used for per-request credential overrides where smart routing is not needed.
/// Skips health pings and scoring — the provider is assumed ready.
/// </summary>
public sealed class ScopedSingleProviderRouter : ISmartRouter
{
    private readonly ILlmProvider _provider;

    /// <summary>Initializes a new instance wrapping a single provider.</summary>
    /// <param name="provider">The sole provider to route all requests to.</param>
    public ScopedSingleProviderRouter(ILlmProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) =>
        Task.FromResult(_provider);

    /// <inheritdoc/>
    public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public IReadOnlyList<ProviderStatus> GetStatus() =>
        [new ProviderStatus(_provider.Name, true, 0, 0, 0, 0, "0%", "scoped")];

    /// <inheritdoc/>
    public Task PinProviderAsync(string? providerName, CancellationToken ct = default) =>
        Task.CompletedTask;
}
