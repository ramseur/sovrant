using System.Reflection;
using System.Text.Json;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Maps Sovrant-internal model identifiers to OpenRouter-format ids
/// (e.g. "claude-sonnet-4-6" → "anthropic/claude-sonnet-4.5").
/// Exact match wins, then glob-style suffix match, then provider-prefix fallback.
/// Unknown ids return <see langword="null"/>.
/// </summary>
public sealed class ModelIdNormaliser
{
    private readonly Dictionary<string, string> _exactMap;
    private readonly List<(string Prefix, string OpenRouterId)> _prefixEntries;

    /// <summary>
    /// Creates a normaliser from the bundled alias table, optionally merged with a user override file.
    /// </summary>
    public ModelIdNormaliser(string? userOverridePath = null)
    {
        _exactMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _prefixEntries = [];

        // Load the bundled alias table from the embedded resource
        LoadFromEmbeddedResource();

        // Merge user overrides (higher priority)
        if (userOverridePath is not null && File.Exists(userOverridePath))
            LoadFromFile(userOverridePath);
    }

    /// <summary>
    /// Normalises a Sovrant model id to an OpenRouter id.
    /// Returns <see langword="null"/> if the model is unknown.
    /// </summary>
    public string? Normalise(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return null;

        // Already in OpenRouter format (has a slash)?
        if (modelId.Contains('/', StringComparison.Ordinal))
            return modelId;

        // Exact match
        if (_exactMap.TryGetValue(modelId, out var exact))
            return exact;

        // Prefix match: e.g. "gpt-4o-2024-11-20" matches prefix "gpt-4o" → "openai/gpt-4o"
        foreach (var (prefix, openRouterId) in _prefixEntries)
        {
            if (modelId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                modelId.Length > prefix.Length &&
                modelId[prefix.Length] == '-')
            {
                return openRouterId;
            }
        }

        return null;
    }

    private void LoadFromEmbeddedResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Sovrant.Runtime.Metrics.OpenRouterModelAliases.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        LoadFromStream(stream);
    }

    private void LoadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        LoadFromStream(stream);
    }

    private void LoadFromStream(Stream stream)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream);
        if (map is null) return;

        foreach (var (openRouterId, aliases) in map)
        {
            foreach (var alias in aliases)
            {
                _exactMap[alias] = openRouterId;

                // Also register prefix entries for date-suffix matching
                // e.g. "gpt-4o" becomes a prefix that matches "gpt-4o-2024-11-20"
                if (!alias.Contains('*', StringComparison.Ordinal) && alias.Length >= 3)
                    _prefixEntries.Add((alias, openRouterId));
            }
        }
    }
}
