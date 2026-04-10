using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Api.Capabilities;

/// <summary>
/// Fetches model capabilities from the OpenRouter <c>/api/v1/models</c>
/// endpoint at startup and seeds them into the registry as
/// <see cref="CapabilitySource.Live"/> entries. These are the lowest
/// priority — bundled and user overrides take precedence.
/// </summary>
public sealed partial class LiveModelMetadataFetcher
{
    private readonly IModelCapabilityRegistry _registry;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveModelMetadataFetcher> _logger;

    /// <summary>OpenRouter's public model listing — no auth required.</summary>
    private const string OpenRouterModelsUrl = "https://openrouter.ai/api/v1/models";

    public LiveModelMetadataFetcher(
        IModelCapabilityRegistry registry,
        HttpClient httpClient,
        ILogger<LiveModelMetadataFetcher> logger)
    {
        _registry = registry;
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches model metadata from OpenRouter and registers capabilities.
    /// Best-effort — failures are logged but do not throw.
    /// </summary>
    public async Task FetchAsync(CancellationToken ct = default)
    {
        // Only fetch if an OpenRouter key is configured (no point otherwise)
        var openRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        if (string.IsNullOrEmpty(openRouterKey))
            return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, OpenRouterModelsUrl);
            request.Headers.Add("HTTP-Referer", "https://github.com/ramseur/sovrant-engine");

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogFetchFailed((int)response.StatusCode);
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var registered = 0;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var model in data.EnumerateArray())
                {
                    if (!model.TryGetProperty("id", out var idProp) || idProp.ValueKind != JsonValueKind.String)
                        continue;

                    var modelId = idProp.GetString();
                    if (string.IsNullOrEmpty(modelId))
                        continue;

                    var caps = ParseModelEntry(model);
                    _registry.Register(modelId, caps);
                    registered++;
                }
            }

            LogFetchComplete(registered);
        }
        catch (HttpRequestException ex)
        {
            LogFetchError(ex);
        }
        catch (TaskCanceledException)
        {
            // Shutdown during fetch — acceptable
        }
        catch (JsonException ex)
        {
            LogFetchError(ex);
        }
    }

    /// <summary>Extracts capabilities from a single OpenRouter model JSON entry.</summary>
    internal static ModelCapabilities ParseModelEntry(JsonElement model)
    {
        var nativeTools = false;
        var structuredOutput = false;
        var thinkingMode = false;
        int? maxContext = null;
        int? maxOutputTokens = null;
        decimal? costInput = null;
        decimal? costOutput = null;

        // Check supported_parameters for tool support, structured output, reasoning
        if (model.TryGetProperty("supported_parameters", out var supported)
            && supported.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in supported.EnumerateArray())
            {
                var paramStr = param.GetString();
                if (string.Equals(paramStr, "tools", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(paramStr, "tool_choice", StringComparison.OrdinalIgnoreCase))
                {
                    nativeTools = true;
                }
                else if (string.Equals(paramStr, "response_format", StringComparison.OrdinalIgnoreCase))
                {
                    structuredOutput = true;
                }
                else if (string.Equals(paramStr, "reasoning", StringComparison.OrdinalIgnoreCase))
                {
                    thinkingMode = true;
                }
            }
        }

        // Context length
        if (model.TryGetProperty("context_length", out var ctxProp)
            && ctxProp.ValueKind == JsonValueKind.Number)
        {
            maxContext = ctxProp.GetInt32();
        }

        // Max completion tokens from top_provider
        if (model.TryGetProperty("top_provider", out var topProvider)
            && topProvider.TryGetProperty("max_completion_tokens", out var maxCompProp)
            && maxCompProp.ValueKind == JsonValueKind.Number)
        {
            maxOutputTokens = maxCompProp.GetInt32();
        }

        // Pricing — OpenRouter returns cost per token as a string (e.g. "0.000003")
        if (model.TryGetProperty("pricing", out var pricing))
        {
            costInput = ParsePricingField(pricing, "prompt");
            costOutput = ParsePricingField(pricing, "completion");
        }

        // Architecture / family
        string? family = null;
        if (model.TryGetProperty("architecture", out var arch))
        {
            if (arch.TryGetProperty("modality", out _))
            {
                // Derive family from model ID prefix
                var id = model.GetProperty("id").GetString() ?? "";
                var slash = id.IndexOf('/', StringComparison.Ordinal);
                if (slash > 0)
                {
                    var modelPart = id[(slash + 1)..];
                    var parts = modelPart.Split('-');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out _))
                        family = $"{parts[0]}-{parts[1]}";
                }
            }
        }

        return new ModelCapabilities
        {
            NativeTools = nativeTools,
            StructuredOutput = structuredOutput,
            ThinkingMode = thinkingMode,
            Family = family,
            MaxContext = maxContext,
            MaxOutputTokens = maxOutputTokens,
            CostPerMillionInput = costInput,
            CostPerMillionOutput = costOutput,
            Source = CapabilitySource.Live,
        };
    }

    /// <summary>
    /// Parses an OpenRouter pricing field. OpenRouter returns cost per token
    /// as a string (e.g. "0.000003"). We convert to cost per million tokens.
    /// </summary>
    private static decimal? ParsePricingField(JsonElement pricing, string fieldName)
    {
        if (!pricing.TryGetProperty(fieldName, out var field))
            return null;

        var raw = field.ValueKind == JsonValueKind.String
            ? field.GetString()
            : field.ValueKind == JsonValueKind.Number
                ? field.GetDecimal().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;

        if (string.IsNullOrEmpty(raw))
            return null;

        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var perToken) && perToken > 0)
        {
            return perToken * 1_000_000m; // Convert per-token → per-million-tokens
        }
        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Live model metadata fetched: {Count} models registered")]
    private partial void LogFetchComplete(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenRouter model metadata: HTTP {StatusCode}")]
    private partial void LogFetchFailed(int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch OpenRouter model metadata")]
    private partial void LogFetchError(Exception ex);
}
