namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Estimates the USD cost of an LLM turn based on model pricing data.
/// Returns <see langword="null"/> when the model is unknown (explicitly not the same as zero).
/// </summary>
public interface ICostModel
{
    /// <summary>
    /// Estimates the cost in USD for the given model and token counts.
    /// </summary>
    /// <param name="model">Sovrant-internal model id (e.g. "claude-sonnet-4-6", "gpt-4o").</param>
    /// <param name="inputTokens">Number of prompt/input tokens.</param>
    /// <param name="outputTokens">Number of completion/output tokens.</param>
    /// <param name="hints">Optional hints for cache tokens, images, etc.</param>
    /// <returns>Estimated USD cost, or <see langword="null"/> if the model is not in the pricing catalogue.</returns>
    decimal? EstimateCost(string model, long inputTokens, long outputTokens, CostHints? hints = null);
}
