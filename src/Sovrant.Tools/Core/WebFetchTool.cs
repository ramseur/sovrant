using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sovrant.Api.Types;

namespace Sovrant.Tools.Core;

/// <summary>Fetches the content of a URL and returns it as text.</summary>
public sealed class WebFetchTool : ITool
{
    private const int DefaultMaxLength = 20_000;
    private static readonly Regex s_htmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled, TimeSpan.FromSeconds(5));
    private static readonly Regex s_whitespaceRegex = new(@"\s{3,}", RegexOptions.Compiled, TimeSpan.FromSeconds(5));

    private static readonly ToolDefinition s_definition = new("WebFetch", CreateSchema())
    {
        Description =
            "Fetches the content of a URL and returns it as plain text. " +
            "HTML tags are stripped. Use max_length to limit the response size.",
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public WebFetchTool(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public ToolDefinition Definition => s_definition;

    public async Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default)
    {
        var url = input.GetStringProp("url");
        if (string.IsNullOrWhiteSpace(url))
            return "Error: url is required.";

        var maxLength = input.GetIntProp("max_length", DefaultMaxLength);

        try
        {
            Uri uri;
            try { uri = new Uri(url); }
            catch (UriFormatException ex) { return $"Error: invalid URL: {ex.Message}"; }

            // Friendly early refusal; the connection itself is also checked (OutboundAddressGuard),
            // which covers hostnames, redirects and DNS rebinding.
            if (OutboundAddressGuard.IsBlockedUri(uri))
                return OutboundAddressGuard.BlockedMessage;

            using var client = _httpClientFactory.CreateClient("WebFetch");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Sovrant/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Stream-based bounded read to avoid loading unbounded responses into memory.
            var maxBytes = maxLength * 4; // generous UTF-8 allowance
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buffer = new byte[Math.Min(maxBytes, 2 * 1024 * 1024)]; // cap at 2 MB read
            int totalRead = 0;
            int bytesRead;
            while (totalRead < buffer.Length &&
                   (bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false)) > 0)
            {
                totalRead += bytesRead;
            }
            var encoding = System.Text.Encoding.UTF8;
            var content = encoding.GetString(buffer, 0, totalRead);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            {
                content = s_htmlTagRegex.Replace(content, " ");
                content = s_whitespaceRegex.Replace(content, " ");
                content = content.Trim();
            }

            if (content.Length > maxLength)
                content = content[..maxLength] + $"\n\n[Truncated — {content.Length:N0} chars total]";

            return content;
        }
        catch (HttpRequestException ex) { return $"Error fetching URL: {ex.Message}"; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return "Error: request timed out.";
        }
    }

    private static JsonElement CreateSchema() => JsonDocument.Parse("""
        {
            "type": "object",
            "properties": {
                "url":        {"type": "string",  "description": "The URL to fetch."},
                "max_length": {"type": "integer", "description": "Maximum characters to return (default 20000)."}
            },
            "required": ["url"]
        }
        """).RootElement;
}
