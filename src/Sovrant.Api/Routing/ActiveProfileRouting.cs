namespace Sovrant.Api.Routing;

/// <summary>
/// Phase 138 — how the active provider profile maps onto the router.
/// </summary>
/// <remarks>
/// Every provider profile, cloud or local (OpenAI, OpenRouter, LM Studio, Ollama, Custom), is
/// served by the router's primary OpenAI-compatible provider at the profile's own base URL
/// (hot-swapped through <c>IBaseUrlOverride</c>), so selecting a profile always pins the primary
/// provider. This replaces the old "base URL is localhost → pin <c>ollama</c>" guess, which sent
/// LM Studio and every other local endpoint to a hard-coded <c>localhost:11434</c>. Ollama is
/// therefore contacted only when an Ollama profile is the active profile, which requires an
/// admin to have enabled it for the workspace.
/// </remarks>
public static class ActiveProfileRouting
{
    /// <summary>
    /// Pins the router to its primary provider (the first registered: <c>openai-compat</c>, or
    /// <c>openai-responses</c> when native web search is on). No-op for a router with no providers.
    /// </summary>
    public static Task PinActiveProfileAsync(this ISmartRouter router, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(router);
        var status = router.GetStatus();
        return status.Count == 0 ? Task.CompletedTask : router.PinProviderAsync(status[0].Name, ct);
    }
}
