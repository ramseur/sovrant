using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Config;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;

namespace Sovrant.Tools.Extended;

/// <summary>
/// Searches the web. Priority: Brave Search → FireCrawl → LLM-native search.
/// When no search API key is set, falls back to the LLM's own web search
/// capabilities (e.g. OpenAI's <c>web_search_preview</c>) or general knowledge.
/// </summary>
public sealed class WebSearchTool : ITool
{
    private const string BraveEndpoint = "https://api.search.brave.com/res/v1/web/search";
    private const string FirecrawlEndpoint = "https://api.firecrawl.dev/v1/search";

    /// <summary>
    /// Whether the LLM fallback for web search is enabled. On by default.
    /// Toggle via <c>/websearch enable</c> or <c>/websearch disable</c>.
    /// </summary>
    public static bool LlmFallbackEnabled { get; set; } = true;

    private static readonly ToolDefinition s_definition = new("WebSearch", CreateSchema())
    {
        Description =
            "Searches the web and returns a list of results with titles, URLs, and snippets. " +
            "Supports Brave Search (BRAVE_API_KEY), FireCrawl (FIRECRAWL_API_KEY), or " +
            "falls back to the LLM's native web search / knowledge when no API key is set. " +
            "Use count to control the number of results (default 5, max 20).",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CredentialConfig? _credentials;
    private readonly ISmartRouter? _router;
    private readonly SovrantConfig? _config;

    public WebSearchTool(IHttpClientFactory httpClientFactory, CredentialConfig? credentials = null, ISmartRouter? router = null, SovrantConfig? config = null)
    {
        _httpClientFactory = httpClientFactory;
        _credentials = credentials;
        _router = router;
        _config = config;
    }

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var query = input.GetStringProp("query");
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query is required.";

        var count = Math.Clamp(input.GetIntProp("count", 5), 1, 20);

        var braveKey = _credentials?.BraveApiKey ?? Environment.GetEnvironmentVariable("BRAVE_API_KEY");
        var firecrawlKey = _credentials?.FirecrawlApiKey ?? Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");

        if (!string.IsNullOrWhiteSpace(braveKey))
            return await SearchBraveAsync(query, count, braveKey, ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(firecrawlKey))
            return await SearchFirecrawlAsync(query, count, firecrawlKey, ct).ConfigureAwait(false);

        // Fall back to LLM-native search (e.g. OpenAI web_search_preview or general knowledge).
        if (LlmFallbackEnabled && _router is not null && _config is not null)
            return await SearchViaLlmAsync(query, count, ct).ConfigureAwait(false);

        return "Error: no search backend available. Set BRAVE_API_KEY or FIRECRAWL_API_KEY, or run /websearch enable to use LLM-based search.";
    }

    private async Task<string> SearchBraveAsync(string query, int count, string apiKey, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("WebSearch");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("X-Subscription-Token", apiKey);

            var url = $"{BraveEndpoint}?q={Uri.EscapeDataString(query)}&count={count}";
            using var response = await client.GetAsync(new Uri(url), ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("web", out var web) &&
                web.TryGetProperty("results", out var results))
            {
                var i = 1;
                foreach (var r in results.EnumerateArray())
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() : string.Empty;
                    var resultUrl = r.TryGetProperty("url", out var u) ? u.GetString() : string.Empty;
                    var desc = r.TryGetProperty("description", out var d) ? d.GetString() : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{i}. {title}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"   {resultUrl}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine(CultureInfo.InvariantCulture, $"   {desc}");
                    sb.AppendLine();
                    i++;
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "No results found.";
        }
        catch (HttpRequestException ex) { return $"Error calling Brave search API: {ex.Message}"; }
        catch (JsonException ex) { return $"Error parsing Brave search response: {ex.Message}"; }
    }

    private async Task<string> SearchFirecrawlAsync(string query, int count, string apiKey, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("WebSearch");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var body = JsonSerializer.Serialize(new { query, limit = count });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(new Uri(FirecrawlEndpoint), content, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                var i = 1;
                foreach (var r in data.EnumerateArray())
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() : string.Empty;
                    var resultUrl = r.TryGetProperty("url", out var u) ? u.GetString() : string.Empty;
                    var desc = r.TryGetProperty("description", out var d) ? d.GetString() : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{i}. {title}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"   {resultUrl}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine(CultureInfo.InvariantCulture, $"   {desc}");
                    sb.AppendLine();
                    i++;
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "No results found.";
        }
        catch (HttpRequestException ex) { return $"Error calling FireCrawl search API: {ex.Message}"; }
        catch (JsonException ex) { return $"Error parsing FireCrawl search response: {ex.Message}"; }
    }



    private async Task<string> SearchViaLlmAsync(string query, int count, CancellationToken ct)
    {
        try
        {
            var req = new MessagesRequest(
                _config!.Model,
                1024,
                [InputMessage.UserText(
                    $"Search the web for: {query}\n\n" +
                    $"Return up to {count} results as a numbered list. " +
                    "For each result include the title, URL (if available), and a brief description. " +
                    "If you have web search capabilities, use them. Otherwise, provide your best " +
                    "knowledge-based answer clearly noting it may not reflect the latest information.")])
            {
                System = "You are a web search assistant. Return concise, factual search results.",
            };

            var provider = await _router!.RouteAsync(req, ct).ConfigureAwait(false);
            var result = await provider.SendAsync(req, ct).ConfigureAwait(false);

            if (!result.IsSuccess || result.Value is null)
                return $"LLM search failed: {result.Error}";

            var text = string.Join("\n",
                result.Value.Content
                    .OfType<OutputContentBlock.TextBlock>()
                    .Select(t => t.Text));

            return string.IsNullOrWhiteSpace(text) ? "No results returned by LLM." : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"LLM search error: {ex.Message}";
        }
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "query": {"type": "string",  "description": "Search query."},
                "count": {"type": "integer", "description": "Number of results (default 5, max 20)."}
            },
            "required": ["query"]
        }
        """).RootElement;
}
