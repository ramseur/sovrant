using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Hooks;

/// <summary>A single hook definition loaded from <c>hooks.json</c>.</summary>
public sealed record HookConfig(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    HookEvent Event,
    string Command,
    HookMatcher? Matcher = null,
    int TimeoutMs = 30_000);

/// <summary>Narrows which tool invocations a hook responds to.</summary>
public sealed record HookMatcher(
    string? Tool = null,
    string? Glob = null);

/// <summary>Root object for <c>hooks.json</c>.</summary>
public sealed class HooksConfig
{
    [JsonPropertyName("hooks")]
    public IReadOnlyList<HookConfig> Hooks { get; init; } = [];
}
