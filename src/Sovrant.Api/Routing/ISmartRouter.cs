using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Routing;

/// <summary>Routes LLM requests across multiple configured providers.</summary>
public interface ISmartRouter
{
    /// <summary>Pings all providers and initialises health and latency data.</summary>
    /// <param name="ct">A cancellation token.</param>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Selects the optimal provider for the given request.</summary>
    /// <param name="req">The messages request (used to estimate request size).</param>
    /// <param name="ct">A cancellation token.</param>
    Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default);

    /// <summary>Records the outcome of a completed request to update provider scoring.</summary>
    /// <param name="providerName">The name of the provider that handled the request.</param>
    /// <param name="success">Whether the request succeeded.</param>
    /// <param name="durationMs">The request duration in milliseconds.</param>
    /// <param name="ct">A cancellation token.</param>
    Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default);

    /// <summary>Returns a snapshot of current health and scoring for all providers.</summary>
    IReadOnlyList<ProviderStatus> GetStatus();

    /// <summary>
    /// Pins all subsequent requests to the named provider, bypassing smart routing.
    /// Pass <see langword="null"/> to unpin and resume normal routing.
    /// Throws <see cref="InvalidOperationException"/> if the provider name is not recognised.
    /// </summary>
    /// <param name="providerName">The provider name to pin, or <see langword="null"/> to unpin.</param>
    /// <param name="ct">A cancellation token.</param>
    Task PinProviderAsync(string? providerName, CancellationToken ct = default);

    /// <summary>
    /// Routes the request and returns extended routing metadata including
    /// the selected provider, intent classification, resolved model, and tier.
    /// </summary>
    Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables intent-aware routing. When <see langword="false"/>,
    /// <see cref="RouteWithIntentAsync"/> behaves like <see cref="RouteAsync"/>.
    /// </summary>
    bool IntentRoutingEnabled { get; set; }
}

/// <summary>The result of intent-aware routing, including the selected provider and metadata.</summary>
/// <param name="Provider">The selected LLM provider.</param>
/// <param name="Classification">The intent classification, or <see langword="null"/> if intent routing is disabled.</param>
/// <param name="ResolvedModel">The model ID selected by the tier resolver, or <see langword="null"/> if the request's original model was used.</param>
/// <param name="Tier">The model tier used, or <see langword="null"/> if not applicable.</param>
public sealed record RoutingDecision(
    ILlmProvider Provider,
    IntentClassification? Classification,
    string? ResolvedModel,
    string? Tier);

/// <summary>A point-in-time snapshot of a provider's health and scoring metrics.</summary>
/// <param name="Name">The provider's name.</param>
/// <param name="Healthy">Whether the provider is currently healthy.</param>
/// <param name="LatencyMs">Current average latency in milliseconds.</param>
/// <param name="CostPer1kTokens">Estimated cost per 1 000 tokens in USD.</param>
/// <param name="RequestCount">Total requests routed to this provider.</param>
/// <param name="ErrorCount">Total errors recorded from this provider.</param>
/// <param name="ErrorRate">Error rate as a formatted percentage string.</param>
/// <param name="Score">Current routing score, or "N/A" if unhealthy.</param>
public sealed record ProviderStatus(
    string Name,
    bool Healthy,
    double LatencyMs,
    double CostPer1kTokens,
    int RequestCount,
    int ErrorCount,
    string ErrorRate,
    string Score);
