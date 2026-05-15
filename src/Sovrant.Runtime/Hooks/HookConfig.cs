using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Hooks;

/// <summary>A single hook definition persisted in the <c>hooks</c> table.</summary>
/// <remarks>
/// <see cref="Id"/> is generated when omitted (empty string) so existing test
/// constructors that pass only Event/Command/Matcher continue to compile.
/// </remarks>
public sealed record HookConfig(
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    HookEvent Event,
    string Command,
    HookMatcher? Matcher = null,
    int TimeoutMs = 30_000,
    string Id = "",
    bool Enabled = true);

/// <summary>Narrows which tool invocations a hook responds to.</summary>
public sealed record HookMatcher(
    string? Tool = null,
    string? Glob = null);
