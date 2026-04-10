using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Capabilities;

/// <summary>
/// Loads model capability overrides from two sources:
/// <list type="number">
///   <item>Bundled <c>model-overrides.json</c> (embedded resource)</item>
///   <item>User <c>~/.sovrant/model-overrides.json</c> (optional)</item>
/// </list>
/// Also parses the <c>SOVRANT_MODEL_CAPABILITIES</c> env var for inline overrides.
/// </summary>
public sealed partial class ModelOverrideLoader
{
    private readonly IModelCapabilityRegistry _registry;
    private readonly ILogger<ModelOverrideLoader> _logger;

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ModelOverrideLoader(
        IModelCapabilityRegistry registry,
        ILogger<ModelOverrideLoader> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Loads all override sources into the registry in priority order
    /// (bundled first, then user, then env — each layer overrides the previous).
    /// </summary>
    public void LoadAll()
    {
        LoadBundled();
        LoadUserFile();
        LoadEnvironmentVariable();
    }

    /// <summary>Loads the embedded <c>model-overrides.json</c> resource.</summary>
    private void LoadBundled()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("model-overrides.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            LogBundledNotFound();
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        LoadFromStream(stream, CapabilitySource.Bundled);
        LogBundledLoaded();
    }

    /// <summary>Loads <c>~/.sovrant/model-overrides.json</c> if it exists.</summary>
    private void LoadUserFile()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "model-overrides.json");

        if (!File.Exists(path)) return;

        try
        {
            using var stream = File.OpenRead(path);
            LoadFromStream(stream, CapabilitySource.User);
            LogUserFileLoaded(path);
        }
        catch (IOException ex)
        {
            LogUserFileError(path, ex);
        }
        catch (JsonException ex)
        {
            LogUserFileError(path, ex);
        }
    }

    /// <summary>
    /// Parses <c>SOVRANT_MODEL_CAPABILITIES</c> env var.
    /// Format: <c>model:key=value,model:key=value</c>
    /// Example: <c>google/gemma-4-27b:native_tools=true,ollama/gemma4:27b:ollama_template_workaround=false</c>
    /// </summary>
    private void LoadEnvironmentVariable()
    {
        var envValue = Environment.GetEnvironmentVariable("SOVRANT_MODEL_CAPABILITIES");
        if (string.IsNullOrWhiteSpace(envValue)) return;

        // Split on comma, then parse each entry as model:key=value
        foreach (var entry in envValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Find the last colon that separates model from key=value
            // Model IDs can contain colons (e.g. gemma4:27b), so we find key=value patterns
            var kvSeparator = entry.LastIndexOf(':');
            if (kvSeparator <= 0) continue;

            var modelId = entry[..kvSeparator];
            var kvPart = entry[(kvSeparator + 1)..];

            var eqIdx = kvPart.IndexOf('=', StringComparison.Ordinal);
            if (eqIdx <= 0) continue;

            var key = kvPart[..eqIdx].Trim();
            var value = kvPart[(eqIdx + 1)..].Trim();

            // Start from defaults, apply the single override
            var existing = _registry.GetCapabilities(modelId);
            var updated = ApplyOverride(existing, key, value);
            if (updated is not null)
            {
                _registry.Register(modelId, updated with { Source = CapabilitySource.User });
                LogEnvOverrideApplied(modelId, key, value);
            }
        }
    }

    private void LoadFromStream(Stream stream, CapabilitySource source)
    {
        var doc = JsonSerializer.Deserialize<OverrideDocument>(stream, s_json);
        if (doc is null) return;

        var now = DateTimeOffset.UtcNow;

        // Load overrides
        if (doc.Overrides is not null)
        {
            foreach (var entry in doc.Overrides)
            {
                if (string.IsNullOrWhiteSpace(entry.Pattern)) continue;

                // Check expiry
                if (entry.Expires is not null && DateTimeOffset.TryParse(entry.Expires, out var expiry) && now > expiry)
                {
                    LogOverrideExpired(entry.Pattern, entry.Expires);
                    continue;
                }

                if (entry.Capabilities is null) continue;

                var caps = entry.Capabilities with { Source = source };
                _registry.Register(entry.Pattern, caps);
            }
        }

        // Load aliases
        if (doc.Aliases is not null)
        {
            foreach (var (alias, canonical) in doc.Aliases)
            {
                _registry.RegisterAlias(alias, canonical);
            }
        }
    }

    private static ModelCapabilities? ApplyOverride(ModelCapabilities existing, string key, string value)
    {
        var boolVal = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        return key switch
        {
            "native_tools" => existing with { NativeTools = boolVal },
            "structured_output" => existing with { StructuredOutput = boolVal },
            "thinking_mode" => existing with { ThinkingMode = boolVal },
            "ollama_template_workaround" => existing with { OllamaTemplateWorkaround = boolVal },
            "family" => existing with { Family = value },
            _ => null,
        };
    }

    // ── JSON models ─────────────────────────────────────────────────────

    private sealed class OverrideDocument
    {
        [JsonPropertyName("overrides")]
        public List<OverrideEntry>? Overrides { get; set; }

        [JsonPropertyName("aliases")]
        public Dictionary<string, string>? Aliases { get; set; }
    }

    private sealed class OverrideEntry
    {
        [JsonPropertyName("pattern")]
        public string? Pattern { get; set; }

        [JsonPropertyName("capabilities")]
        public ModelCapabilities? Capabilities { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("expires")]
        public string? Expires { get; set; }
    }

    // ── Logging ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bundled model overrides loaded")]
    private partial void LogBundledLoaded();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bundled model-overrides.json not found in assembly resources")]
    private partial void LogBundledNotFound();

    [LoggerMessage(Level = LogLevel.Information, Message = "User model overrides loaded from {Path}")]
    private partial void LogUserFileLoaded(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load user model overrides from {Path}")]
    private partial void LogUserFileError(string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Override for {Pattern} expired on {Expires}, skipping")]
    private partial void LogOverrideExpired(string pattern, string expires);

    [LoggerMessage(Level = LogLevel.Information, Message = "Env override applied: {ModelId} {Key}={Value}")]
    private partial void LogEnvOverrideApplied(string modelId, string key, string value);
}
