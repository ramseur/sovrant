using Sovrant.Api.Providers;

namespace Sovrant.Api.Routing;

/// <summary>Metadata and runtime health tracking for a single LLM provider endpoint.</summary>
public sealed class ProviderInfo
{
    private readonly object _statsLock = new();
    private int _requestCount;
    private int _errorCount;

    /// <summary>Initializes a new instance of <see cref="ProviderInfo"/>.</summary>
    /// <param name="provider">The underlying provider implementation.</param>
    /// <param name="pingPath">Path used for health-check pings (e.g. "/v1/models").</param>
    /// <param name="costPer1kTokens">Estimated cost in USD per 1 000 tokens.</param>
    public ProviderInfo(ILlmProvider provider, string pingPath, double costPer1kTokens)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(pingPath);
        Provider = provider;
        PingPath = pingPath;
        CostPer1kTokens = costPer1kTokens;
    }

    /// <summary>The underlying provider implementation.</summary>
    public ILlmProvider Provider { get; }

    /// <summary>Health-check path (e.g. "/v1/models" or "/api/tags" for Ollama).</summary>
    public string PingPath { get; }

    /// <summary>Estimated cost per 1 000 tokens in USD.</summary>
    public double CostPer1kTokens { get; }

    private volatile bool _healthy = true;

    /// <summary>Whether this provider is currently considered healthy.</summary>
    public bool Healthy { get => _healthy; set => _healthy = value; }

    /// <summary>Most recent ping latency in milliseconds.</summary>
    public double LatencyMs { get; set; } = 9999.0;

    /// <summary>Exponential-moving-average latency in milliseconds.</summary>
    public double AvgLatencyMs { get; set; } = 9999.0;

    /// <summary>Total requests routed to this provider.</summary>
    public int RequestCount => _requestCount;

    /// <summary>Total errors recorded from this provider.</summary>
    public int ErrorCount => _errorCount;

    /// <summary>Current error rate as a value between 0 and 1.</summary>
    public double ErrorRate
    {
        get
        {
            var total = _requestCount;
            return total == 0 ? 0.0 : (double)_errorCount / total;
        }
    }

    /// <summary>Atomically increments the request counter and returns the new value.</summary>
    public int IncrementRequests() => Interlocked.Increment(ref _requestCount);

    /// <summary>Atomically increments the error counter and returns the new value.</summary>
    public int IncrementErrors() => Interlocked.Increment(ref _errorCount);

    /// <summary>Updates the average latency under a lock to prevent read-modify-write races.</summary>
    public void UpdateAvgLatency(double durationMs)
    {
        const double alpha = 0.3;
        lock (_statsLock)
        {
            AvgLatencyMs = alpha * durationMs + (1 - alpha) * AvgLatencyMs;
        }
    }

    /// <summary>
    /// Computes a routing score for this provider. Lower score = better candidate.
    /// </summary>
    /// <param name="strategy">The scoring strategy to apply.</param>
    public double Score(RouterStrategy strategy)
    {
        if (!Healthy) return double.MaxValue;
        var latencyScore = AvgLatencyMs / 1000.0;
        var costScore = CostPer1kTokens * 100.0;
        var errorPenalty = ErrorRate * 500.0;
        return strategy switch
        {
            RouterStrategy.Latency => latencyScore + errorPenalty,
            RouterStrategy.Cost => costScore + errorPenalty,
            _ => latencyScore * 0.5 + costScore * 0.5 + errorPenalty
        };
    }
}
