using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Sovrant.Runtime.Session;

namespace Sovrant.Client.Remote;

/// <summary>
/// Phase 133 — <see cref="ISessionFolderStore"/> over the server's
/// <c>/v1/session-folders</c> endpoints. The server always acts on the
/// authenticated caller's own tree, so <c>ownerUserId</c> is not sent; rule
/// refusals come back as <see cref="SessionFolderException"/> with the same
/// <see cref="SessionFolderError"/> the embedded stores would raise.
/// </summary>
public sealed class RemoteSessionFolderStore : ISessionFolderStore
{
    private readonly HttpClient _http;

    public RemoteSessionFolderStore(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        _http = httpClientFactory.CreateClient("SovrantApi");
    }

    public async Task<IReadOnlyList<SessionFolder>> ListAsync(string ownerUserId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(new Uri("/v1/session-folders", UriKind.Relative), ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return doc.RootElement.TryGetProperty("folders", out var arr)
            ? arr.EnumerateArray().Select(f => Read(f, ownerUserId)).ToList()
            : [];
    }

    public async Task<SessionFolder> CreateAsync(string ownerUserId, string name, string? parentFolderId, CancellationToken ct = default)
    {
        using var response = await _http.PostAsJsonAsync(new Uri("/v1/session-folders", UriKind.Relative),
            new { name, parent_folder_id = parentFolderId }, ct).ConfigureAwait(false);
        return await ReadFolderAsync(response, ownerUserId, ct).ConfigureAwait(false);
    }

    public async Task<SessionFolder> RenameAsync(string ownerUserId, string folderId, string name, CancellationToken ct = default)
    {
        using var response = await PatchAsync(folderId, JsonSerializer.Serialize(new { name }), ct).ConfigureAwait(false);
        return await ReadFolderAsync(response, ownerUserId, ct).ConfigureAwait(false);
    }

    public async Task<SessionFolder> MoveAsync(string ownerUserId, string folderId, string? newParentFolderId, CancellationToken ct = default)
    {
        // Serialized explicitly so a null parent is sent as "parent_folder_id": null (a move to the top level).
        using var response = await PatchAsync(folderId, JsonSerializer.Serialize(new { parent_folder_id = newParentFolderId }), ct).ConfigureAwait(false);
        return await ReadFolderAsync(response, ownerUserId, ct).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(string ownerUserId, string folderId, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync(FolderUri(folderId), ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MoveSessionAsync(string ownerUserId, string sessionId, string? folderId, CancellationToken ct = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { folder_id = folderId }), Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync(
            new Uri($"/v1/sessions/{Uri.EscapeDataString(sessionId)}/folder", UriKind.Relative), content, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
            return true;
        // Folder refusals (including a missing folder) carry a "code"; a missing or
        // someone else's conversation is a plain 404 without one — that's just "false".
        if (await ReadErrorAsync(response, ct).ConfigureAwait(false) is { } error)
            throw new SessionFolderException(error);
        return false;
    }

    private async Task<HttpResponseMessage> PatchAsync(string folderId, string json, CancellationToken ct)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _http.PatchAsync(FolderUri(folderId), content, ct).ConfigureAwait(false);
    }

    private static Uri FolderUri(string folderId) =>
        new($"/v1/session-folders/{Uri.EscapeDataString(folderId)}", UriKind.Relative);

    private static async Task<SessionFolder> ReadFolderAsync(HttpResponseMessage response, string ownerUserId, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
            throw new SessionFolderException(await ReadErrorAsync(response, ct).ConfigureAwait(false) ?? SessionFolderError.NotFound);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return Read(doc.RootElement, ownerUserId);
    }

    /// <summary>Maps the server's <c>code</c> field back to a <see cref="SessionFolderError"/>.</summary>
    private static async Task<SessionFolderError?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() : null;
            return code switch
            {
                "not_found" => SessionFolderError.NotFound,
                "invalid_name" => SessionFolderError.InvalidName,
                "duplicate_name" => SessionFolderError.DuplicateName,
                "too_deep" => SessionFolderError.TooDeep,
                "cycle" => SessionFolderError.Cycle,
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SessionFolder Read(JsonElement f, string ownerUserId) => new(
        FolderId: f.GetProperty("folder_id").GetString()!,
        OwnerUserId: ownerUserId,
        ParentFolderId: f.TryGetProperty("parent_folder_id", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null,
        Name: f.GetProperty("name").GetString() ?? string.Empty,
        SortOrder: f.TryGetProperty("sort_order", out var o) ? o.GetInt32() : 0,
        CreatedAt: Date(f, "created_at"),
        UpdatedAt: Date(f, "updated_at"));

    private static DateTimeOffset Date(JsonElement f, string name) =>
        f.TryGetProperty(name, out var d) && d.GetString() is { } s
            ? DateTimeOffset.Parse(s, CultureInfo.InvariantCulture)
            : DateTimeOffset.MinValue;
}

/// <summary>
/// Phase 133 — in remote mode the server already labels every row of
/// <c>GET /v1/sessions</c>, and <see cref="RemoteSessionStore"/> reads those
/// labels, so there is nothing left to resolve client-side.
/// </summary>
public sealed class RemoteSessionLinkResolver : ISessionLinkResolver
{
    public Task<IReadOnlyList<SessionListItem>> WithLabelsAsync(IReadOnlyList<SessionListItem> items, CancellationToken ct = default) =>
        Task.FromResult(items);
}
