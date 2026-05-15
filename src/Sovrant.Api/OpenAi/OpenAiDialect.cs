namespace Sovrant.Api.OpenAi;

/// <summary>
/// Distinguishes between OpenAI-compatible endpoints that share the same
/// <c>/chat/completions</c> wire shape but differ in how they accept
/// native web search. Resolved from the active base URL.
/// </summary>
internal enum OpenAiDialect
{
    /// <summary>Direct OpenAI API (<c>api.openai.com</c>). Native search lives on the Responses API, not Chat Completions.</summary>
    OpenAi,

    /// <summary>OpenRouter (<c>openrouter.ai</c>). Accepts a top-level <c>plugins:[{id:"web"}]</c> field.</summary>
    OpenRouter,

    /// <summary>Google Gemini's OpenAI-compat shim (<c>generativelanguage.googleapis.com</c>). Reserved for a future roadmap entry.</summary>
    GeminiShim,

    /// <summary>Any other OpenAI-compatible endpoint (Ollama, Groq, Together, vLLM, etc.). No native web search.</summary>
    Other,
}

/// <summary>Resolves <see cref="OpenAiDialect"/> from a base URL.</summary>
internal static class OpenAiDialectResolver
{
    /// <summary>
    /// Maps a base URL to its dialect. Matching is host-substring based and
    /// case-insensitive — the URL only needs to contain the marker.
    /// </summary>
    public static OpenAiDialect Resolve(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return OpenAiDialect.Other;
        if (baseUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase))
            return OpenAiDialect.OpenRouter;
        if (baseUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            return OpenAiDialect.GeminiShim;
        if (baseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
            return OpenAiDialect.OpenAi;
        return OpenAiDialect.Other;
    }
}
