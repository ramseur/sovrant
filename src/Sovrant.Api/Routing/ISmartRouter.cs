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
}

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
