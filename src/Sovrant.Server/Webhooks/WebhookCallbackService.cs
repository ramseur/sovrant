using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Server.Webhooks;

/// <summary>
/// Posts webhook responses to external callback URLs (Slack, Teams, Discord, custom).
/// Uses a named <see cref="HttpClient"/> to avoid socket exhaustion.
/// </summary>
public sealed class WebhookCallbackService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookCallbackService> _logger;

    public WebhookCallbackService(
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookCallbackService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// POSTs the webhook response to the callback URL. Errors are logged but do not
    /// propagate — the agent turn has already completed and the result is authoritative.
    /// </summary>
    public async Task DeliverAsync(string callbackUrl, WebhookResponse response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callbackUrl);
        ArgumentNullException.ThrowIfNull(response);

        try
        {
            var client = _httpClientFactory.CreateClient("WebhookCallback");
            var json = JsonSerializer.Serialize(response, s_jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpResponse = await client.PostAsync(new Uri(callbackUrl), content, ct)
                .ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Webhook callback to {CallbackUrl} returned {StatusCode}.",
                    callbackUrl,
                    (int)httpResponse.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            _logger.LogError(ex, "Failed to deliver webhook callback to {CallbackUrl}.", callbackUrl);
        }
    }
}
