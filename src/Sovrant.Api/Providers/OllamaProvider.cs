using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Api.OpenAi;

namespace Sovrant.Api.Providers;

/// <summary>LLM provider for local Ollama instances (OpenAI-compat format, no authentication required).</summary>
public sealed class OllamaProvider : OpenAiCompatProvider
{
    /// <summary>Initializes a new instance of <see cref="OllamaProvider"/>.</summary>
    /// <param name="http">The HTTP client pointed at the Ollama base URL.</param>
    /// <param name="logger">The logger.</param>
    public OllamaProvider(HttpClient http, ILogger<OllamaProvider> logger)
        : base(http, new Auth.ApiKeyAuthProvider(string.Empty), logger) { }

    /// <inheritdoc/>
    public override string Name => "ollama";

    /// <inheritdoc/>
    private protected override Task<HttpRequestMessage> BuildRequestAsync(OpenAiChatRequest openAiReq, CancellationToken ct)
    {
        // Ollama requires no Authorization header
        var json = JsonSerializer.Serialize(openAiReq, SovrantJsonContext.Default.OpenAiChatRequest);
        var httpReq = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(httpReq);
    }
}
