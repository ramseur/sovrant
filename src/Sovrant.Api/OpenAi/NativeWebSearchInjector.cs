using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;

namespace Sovrant.Api.OpenAi;

/// <summary>
/// Single decision point for whether the runtime should inject a
/// provider-native web-search tool into the outgoing LLM request, and
/// whether the function-tool form of <c>WebSearch</c> should be
/// suppressed at the same time.
/// </summary>
/// <param name="InjectNative">
/// When <see langword="true"/>, providers that support a native
/// server-side search tool should add it to the request:
/// OpenAI Responses → <c>web_search_preview</c>;
/// OpenRouter → <c>plugins:[{id:"web"}]</c>;
/// Anthropic → <c>web_search_20250305</c>.
/// </param>
/// <param name="SuppressFunctionTool">
/// When <see langword="true"/>, drop the function-tool form of
/// <c>WebSearch</c> from the request. Set whenever native search is
/// actively handling the role to avoid the model picking the wrong path.
/// </param>
/// <param name="Reason">
/// Diagnostic string explaining the decision — surfaced by telemetry
/// and by the future Settings UI when a backend is requested but
/// unavailable.
/// </param>
internal sealed record NativeWebSearchPlan(
    bool InjectNative,
    bool SuppressFunctionTool,
    string Reason);

/// <summary>
/// Resolves a <see cref="NativeWebSearchPlan"/> from the user's
/// configured backend, the active model's capabilities, and which
/// third-party search keys are available. Pure function — safe to
/// call per request.
/// </summary>
internal static class NativeWebSearchInjector
{
    /// <summary>
    /// Computes the plan. The decision matrix:
    /// <list type="bullet">
    /// <item><c>Off</c> → suppress function tool, no native.</item>
    /// <item><c>Brave</c> / <c>Firecrawl</c> → keep function tool active, no native (the WebSearch tool will hit the third-party API).</item>
    /// <item><c>Native</c> → inject native and suppress the function tool, regardless of model support (caller surfaces a warning if unsupported).</item>
    /// <item><c>Auto</c> → if Brave or Firecrawl key present, behave like that backend; else if model supports native, inject native; else neither.</item>
    /// <item><c>SearxngFuture</c> → behaves like <c>Auto</c> until the SearXNG provider lands.</item>
    /// </list>
    /// </summary>
    /// <param name="model">The model ID being targeted by the request.</param>
    /// <param name="options">Resolved <see cref="WebSearchOptions"/> (or a session override).</param>
    /// <param name="capabilities">Capability registry — used to look up <see cref="ModelCapabilities.SupportsNativeWebSearch"/>.</param>
    /// <param name="hasBraveKey">Whether <c>BRAVE_API_KEY</c> is configured.</param>
    /// <param name="hasFirecrawlKey">Whether <c>FIRECRAWL_API_KEY</c> is configured.</param>
    public static NativeWebSearchPlan Plan(
        string model,
        WebSearchOptions options,
        IModelCapabilityRegistry? capabilities,
        bool hasBraveKey,
        bool hasFirecrawlKey)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool modelSupportsNative = capabilities is not null
            && capabilities.GetCapabilities(model).SupportsNativeWebSearch;

        switch (options.Backend)
        {
            case WebSearchBackend.Off:
                return new NativeWebSearchPlan(false, true, "backend=off");

            case WebSearchBackend.Brave:
                return new NativeWebSearchPlan(false, false, "backend=brave");

            case WebSearchBackend.Firecrawl:
                return new NativeWebSearchPlan(false, false, "backend=firecrawl");

            case WebSearchBackend.Native:
                return new NativeWebSearchPlan(
                    true,
                    true,
                    modelSupportsNative
                        ? "backend=native (model supports)"
                        : "backend=native (model unsupported — request may error)");

            case WebSearchBackend.Auto:
            case WebSearchBackend.SearxngFuture:
            default:
                if (hasBraveKey)
                    return new NativeWebSearchPlan(false, false, "auto: brave key present");
                if (hasFirecrawlKey)
                    return new NativeWebSearchPlan(false, false, "auto: firecrawl key present");
                if (modelSupportsNative)
                    return new NativeWebSearchPlan(true, true, "auto: model supports native");
                return new NativeWebSearchPlan(false, false, "auto: no provider available");
        }
    }
}
