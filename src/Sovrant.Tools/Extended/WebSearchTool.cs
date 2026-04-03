using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Extended;

/// <summary>Searches the web using the Brave Search API (BRAVE_API_KEY env var).</summary>
public sealed class WebSearchTool : ITool
{
    private const string BraveEndpoint = "https://api.search.brave.com/res/v1/web/search";

    private static readonly ToolDefinition s_definition = new("WebSearch", CreateSchema())
    {
        Description =
            "Searches the web and returns a list of results with titles, URLs, and snippets. " +
            "Requires BRAVE_API_KEY environment variable. " +
            "Use count to control the number of results (default 5, max 20).",
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public WebSearchTool(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var query = GetString(input, "query");
        if (string.IsNullOrWhiteSpace(query))
            return "Error: query is required.";

        var count = Math.Clamp(GetInt(input, "count", 5), 1, 20);
        var apiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Error: BRAVE_API_KEY environment variable is not set.";

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
                    var url2 = r.TryGetProperty("url", out var u) ? u.GetString() : string.Empty;
                    var desc = r.TryGetProperty("description", out var d) ? d.GetString() : string.Empty;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"{i}. {title}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"   {url2}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine(CultureInfo.InvariantCulture, $"   {desc}");
                    sb.AppendLine();
                    i++;
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "No results found.";
        }
        catch (HttpRequestException ex) { return $"Error calling search API: {ex.Message}"; }
        catch (JsonException ex) { return $"Error parsing search response: {ex.Message}"; }
    }

    private static string GetString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out var v) ? v.GetString() ?? def : def;

    private static int GetInt(JsonElement el, string prop, int def) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var n) ? n : def;

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
