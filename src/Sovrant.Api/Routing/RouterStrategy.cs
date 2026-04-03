namespace Sovrant.Api.Routing;

/// <summary>Scoring strategy used by the SmartRouter when in Smart mode.</summary>
public enum RouterStrategy
{
    /// <summary>Optimise for lowest response latency.</summary>
    Latency,
    /// <summary>Optimise for lowest cost per token.</summary>
    Cost,
    /// <summary>Balance latency and cost equally.</summary>
    Balanced
}
