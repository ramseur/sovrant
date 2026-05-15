using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Capabilities;

/// <summary>
/// Layered model capability registry. Resolution order (highest wins):
/// user overrides → bundled overrides → live provider metadata → defaults.
/// Supports glob patterns (e.g. <c>google/gemma-4-*</c>) for matching
/// model families.
/// </summary>
public sealed partial class ModelCapabilityRegistry : IModelCapabilityRegistry
{
    private readonly Dictionary<string, ModelCapabilities> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Pattern, Regex Regex, ModelCapabilities Caps)> _globs = [];
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ModelCapabilityRegistry> _logger;
    private readonly object _lock = new();

    private static readonly ModelCapabilities s_default = new()
    {
        NativeTools = true,
        StructuredOutput = true,
        Source = CapabilitySource.Default,
    };

    public ModelCapabilityRegistry(ILogger<ModelCapabilityRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public ModelCapabilities GetCapabilities(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);

        // Normalize first
        var normalized = Normalize(modelId);

        lock (_lock)
        {
            // Exact match — highest-source wins (already handled at registration time)
            if (_exact.TryGetValue(normalized, out var exact))
                return exact;

            // Glob match — first match wins (globs are ordered by source priority)
            foreach (var (_, regex, caps) in _globs)
            {
                if (regex.IsMatch(normalized))
                    return caps;
            }
        }

        return s_default;
    }

    /// <inheritdoc/>
    public void Register(string modelIdOrPattern, ModelCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(modelIdOrPattern);
        ArgumentNullException.ThrowIfNull(capabilities);

        lock (_lock)
        {
            if (modelIdOrPattern.Contains('*', StringComparison.Ordinal))
            {
                // Glob pattern — convert to regex
                var regex = GlobToRegex(modelIdOrPattern);

                // Remove existing glob with same pattern if new source is >= priority
                _globs.RemoveAll(g =>
                    string.Equals(g.Pattern, modelIdOrPattern, StringComparison.OrdinalIgnoreCase)
                    && capabilities.Source >= g.Caps.Source);

                _globs.Add((modelIdOrPattern, regex, capabilities));

                // Sort by source descending so higher-priority globs match first
                _globs.Sort((a, b) => b.Caps.Source.CompareTo(a.Caps.Source));

                LogGlobRegistered(modelIdOrPattern, capabilities.Source);
            }
            else
            {
                // Exact match — only overwrite if new source is >= priority
                if (_exact.TryGetValue(modelIdOrPattern, out var existing)
                    && capabilities.Source < existing.Source)
                {
                    LogOverrideSkipped(modelIdOrPattern, capabilities.Source, existing.Source);
                    return;
                }

                _exact[modelIdOrPattern] = capabilities;
                LogModelRegistered(modelIdOrPattern, capabilities.Source);
            }
        }
    }

    /// <inheritdoc/>
    public string Normalize(string modelId)
    {
        ArgumentNullException.ThrowIfNull(modelId);

        lock (_lock)
        {
            return _aliases.TryGetValue(modelId, out var canonical) ? canonical : modelId;
        }
    }

    /// <inheritdoc/>
    public void RegisterAlias(string aliasId, string canonicalId)
    {
        ArgumentNullException.ThrowIfNull(aliasId);
        ArgumentNullException.ThrowIfNull(canonicalId);

        lock (_lock)
        {
            _aliases[aliasId] = canonicalId;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ModelCapabilities> GetAll()
    {
        lock (_lock)
        {
            return new Dictionary<string, ModelCapabilities>(_exact, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Converts a glob pattern (e.g. <c>google/gemma-4-*</c>) to a
    /// compiled regex. Only <c>*</c> is supported as a wildcard.
    /// </summary>
    private static Regex GlobToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal);
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Model capability registered: {ModelId} (source={Source})")]
    private partial void LogModelRegistered(string modelId, CapabilitySource source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Model capability glob registered: {Pattern} (source={Source})")]
    private partial void LogGlobRegistered(string pattern, CapabilitySource source);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Model capability override skipped for {ModelId}: {NewSource} < existing {ExistingSource}")]
    private partial void LogOverrideSkipped(string modelId, CapabilitySource newSource, CapabilitySource existingSource);
}
