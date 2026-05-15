namespace Sovrant.Api.Capabilities;

/// <summary>
/// Answers capability questions about any model ID. Resolution order
/// (highest wins): user overrides → bundled overrides → live provider
/// metadata → defaults.
/// </summary>
public interface IModelCapabilityRegistry
{
    /// <summary>
    /// Returns the resolved capabilities for the given model ID.
    /// Never returns <see langword="null"/> — unknown models get defaults.
    /// </summary>
    ModelCapabilities GetCapabilities(string modelId);

    /// <summary>
    /// Registers a capability entry. Higher <see cref="CapabilitySource"/>
    /// values override lower ones for the same model ID.
    /// </summary>
    void Register(string modelIdOrPattern, ModelCapabilities capabilities);

    /// <summary>
    /// Normalizes a model ID to its canonical form (e.g.
    /// <c>gemma4:27b</c> → <c>google/gemma-4-27b</c>).
    /// Returns the input unchanged if no alias is registered.
    /// </summary>
    string Normalize(string modelId);

    /// <summary>Registers a model ID alias mapping.</summary>
    void RegisterAlias(string aliasId, string canonicalId);

    /// <summary>Returns all registered model IDs and their capabilities.</summary>
    IReadOnlyDictionary<string, ModelCapabilities> GetAll();
}
