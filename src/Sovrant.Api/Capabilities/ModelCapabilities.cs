using System.Text.Json.Serialization;

namespace Sovrant.Api.Capabilities;

/// <summary>
/// Describes what a model can do. Resolved by the
/// <see cref="IModelCapabilityRegistry"/> from layered sources:
/// user overrides → bundled overrides → live provider metadata → defaults.
/// </summary>
public sealed record ModelCapabilities
{
    /// <summary>Whether the model supports native tool/function calling.</summary>
    [JsonPropertyName("native_tools")]
    public bool NativeTools { get; init; } = true;

    /// <summary>Whether the model supports structured JSON output mode.</summary>
    [JsonPropertyName("structured_output")]
    public bool StructuredOutput { get; init; } = true;

    /// <summary>Whether the model supports an extended thinking / reasoning channel.</summary>
    [JsonPropertyName("thinking_mode")]
    public bool ThinkingMode { get; init; }

    /// <summary>
    /// When <see langword="true"/>, the Ollama provider wraps requests
    /// with a raw-prompt adapter that bypasses Ollama's broken chat
    /// template for this model. Temporary — auto-disables via override
    /// once upstream ships the fix.
    /// </summary>
    [JsonPropertyName("ollama_template_workaround")]
    public bool OllamaTemplateWorkaround { get; init; }

    /// <summary>
    /// Canonical model family (e.g. "gemma-4", "gpt-4", "claude-4").
    /// Used by the cost layer and alias normalizer to group variants.
    /// </summary>
    [JsonPropertyName("family")]
    public string? Family { get; init; }

    /// <summary>Maximum context window in tokens, if known.</summary>
    [JsonPropertyName("max_context")]
    public int? MaxContext { get; init; }

    /// <summary>
    /// Where this capability record came from.
    /// Higher-priority sources override lower.
    /// </summary>
    [JsonPropertyName("source")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CapabilitySource Source { get; init; }
}

/// <summary>Indicates where a <see cref="ModelCapabilities"/> record originated.</summary>
public enum CapabilitySource
{
    /// <summary>Assumed defaults — no explicit data available.</summary>
    Default = 0,

    /// <summary>Fetched live from the provider's model listing API.</summary>
    Live = 1,

    /// <summary>From the bundled <c>model-overrides.json</c> shipped with the binary.</summary>
    Bundled = 2,

    /// <summary>From the user's <c>~/.sovrant/model-overrides.json</c> or env var.</summary>
    User = 3,
}
