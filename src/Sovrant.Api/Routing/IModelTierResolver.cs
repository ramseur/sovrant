namespace Sovrant.Api.Routing;

/// <summary>
/// Resolves a model tier name (<c>fast</c>, <c>standard</c>, <c>high</c>)
/// to a concrete model ID from the user's discovered fleet.
/// </summary>
public interface IModelTierResolver
{
    /// <summary>
    /// Resolves the given tier to a concrete model ID, optionally boosted
    /// by an intent class for models with <see cref="Capabilities.ModelCapabilities.IntentAffinity"/>.
    /// When <paramref name="requireTools"/> is <c>true</c>, only models with
    /// <see cref="Capabilities.ModelCapabilities.NativeTools"/> support are eligible.
    /// Returns <see langword="null"/> if no model is available for the requested tier.
    /// </summary>
    string? Resolve(string tier, IntentClass? intent = null, bool requireTools = false);

    /// <summary>
    /// Returns the current tier assignments: tier name → list of (modelId, score) entries,
    /// sorted by score ascending (best first).
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<TierCandidate>> GetTierAssignments();

    /// <summary>
    /// Rebuilds tier assignments from the current model capability registry.
    /// Called at startup and on provider re-discovery.
    /// </summary>
    void Rebuild();
}

/// <summary>A model candidate within a tier, with its routing score.</summary>
/// <param name="ModelId">The canonical model identifier.</param>
/// <param name="Score">Routing score — lower is better (cheaper, faster).</param>
/// <param name="TierHint">The tier this model was assigned to.</param>
public sealed record TierCandidate(string ModelId, decimal Score, string TierHint);
