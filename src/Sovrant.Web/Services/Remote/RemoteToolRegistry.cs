using System.Text.Json;
using Sovrant.Api.Types;
using Sovrant.Runtime.Tools;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// Read-only <see cref="IToolRegistry"/> backed by <c>GET /v1/tools</c>.
/// Registration is a no-op — tools are managed server-side.
/// </summary>
public sealed class RemoteToolRegistry : IToolRegistry
{
    private readonly HttpClient _http;
    private IReadOnlyList<ToolDefinition>? _cached;

    public RemoteToolRegistry(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("SovrantApi");
    }

    public void Register(ToolDefinition definition, Func<JsonElement, CancellationToken, Task<string>> handler)
    {
        // No-op in remote mode — the server owns tool registration.
    }

    public IReadOnlyList<ToolDefinition> GetDefinitions()
    {
        // Return cached definitions; refresh via RefreshAsync.
        return _cached ?? [];
    }

    public bool TryGetHandler(string name, out Func<JsonElement, CancellationToken, Task<string>>? handler)
    {
        // Remote mode — tools execute server-side only.
        handler = null;
        return false;
    }

    /// <summary>Fetches tool definitions from the server and caches them locally.</summary>
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(new Uri("/v1/tools", UriKind.Relative), ct);
            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var tools = new List<ToolDefinition>();
            if (doc.RootElement.TryGetProperty("tools", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    var schema = item.TryGetProperty("input_schema", out var s) ? s.Clone() : default;
                    var description = item.TryGetProperty("description", out var desc)
                        ? desc.GetString()
                        : null;

                    tools.Add(new ToolDefinition(name, schema) { Description = description });
                }
            }

            _cached = tools;
        }
        catch
        {
            // Silently fail — stale cache is better than crashing.
        }
    }
}
