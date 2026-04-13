using System.Text.Json;
using Sovrant.Runtime.Session;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// <see cref="ISessionStore"/> implementation backed by the Sovrant server REST API.
/// </summary>
public sealed class RemoteSessionStore : ISessionStore
{
    private readonly HttpClient _http;

    public RemoteSessionStore(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("SovrantApi");
    }

    public Task AppendAsync(string sessionId, SessionEntry entry, string? ownerUserId = null, CancellationToken ct = default)
    {
        // The server handles appends internally during RunTurnAsync — this is a no-op
        // in remote mode since the client doesn't directly write session entries.
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(new Uri($"/v1/sessions/{Uri.EscapeDataString(sessionId)}", UriKind.Relative), ct);
        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var entries = new List<SessionEntry>();
        if (doc.RootElement.TryGetProperty("entries", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                var timestamp = item.TryGetProperty("timestamp", out var ts) ? ts.GetDateTimeOffset() : DateTimeOffset.UtcNow;
                var role = item.GetProperty("role").GetString() ?? "user";
                var content = item.GetProperty("content").GetString() ?? string.Empty;
                entries.Add(new SessionEntry(id, timestamp, role, content));
            }
        }

        return entries;
    }

    public async Task<IReadOnlyList<string>> ListAsync(string? ownerUserId = null, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(new Uri("/v1/sessions", UriKind.Relative), ct);
        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        var ids = new List<string>();
        if (doc.RootElement.TryGetProperty("sessions", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.GetProperty("session_id").GetString();
                if (id is not null)
                    ids.Add(id);
            }
        }

        return ids;
    }

    public async Task<bool> DeleteAsync(string sessionId, string? ownerUserId = null, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(new Uri($"/v1/sessions/{Uri.EscapeDataString(sessionId)}", UriKind.Relative), ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        var sessions = await ListAsync(ct: ct);
        var count = 0;
        foreach (var id in sessions)
        {
            if (await DeleteAsync(id, ct: ct))
                count++;
        }
        return count;
    }

    public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
    {
        // Owner resolution happens server-side; the client doesn't need this.
        return Task.FromResult<string?>(null);
    }
}
