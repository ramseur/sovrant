using System.Runtime.CompilerServices;
using System.Text.Json;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Client.Remote;

/// <summary>
/// <see cref="IArtifactStore"/> backed by the Sovrant server <c>/v1/artifacts</c> endpoints.
/// </summary>
public sealed class RemoteArtifactStore : IArtifactStore
{
    private readonly HttpClient _http;

    public RemoteArtifactStore(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient("SovrantApi");
    }

    public Task<ArtifactHandle> CreateRunScopeAsync(ArtifactScope scope, CancellationToken ct = default)
    {
        // In remote mode, the server creates run scopes during agent execution.
        return Task.FromResult(new ArtifactHandle { Scope = scope });
    }

    public Task WriteAsync(ArtifactHandle handle, string relativePath, Stream content, string? contentType = null, CancellationToken ct = default)
    {
        // Writing artifacts happens server-side during agent runs.
        return Task.CompletedTask;
    }

    public async Task<Stream> ReadAsync(ArtifactHandle handle, string relativePath, CancellationToken ct = default)
    {
        var runId = handle.Scope.RunId ?? "unknown";
        var response = await _http.GetAsync(new Uri($"/v1/artifacts/{Uri.EscapeDataString(runId)}/{relativePath}", UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async IAsyncEnumerable<ArtifactEntry> ListAsync(ArtifactScope scope, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var response = await _http.GetAsync(new Uri("/v1/artifacts", UriKind.Relative), ct);
        if (!response.IsSuccessStatusCode)
            yield break;

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("artifacts", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var path = item.TryGetProperty("relative_path", out var p) ? p.GetString() ?? string.Empty : string.Empty;
                var size = item.TryGetProperty("size_bytes", out var s) ? s.GetInt64() : 0;
                var contentType = item.TryGetProperty("content_type", out var c) ? c.GetString() : null;
                var runId = item.TryGetProperty("run_id", out var r) ? r.GetString() : null;
                var lastModified = item.TryGetProperty("last_modified", out var lm) && lm.TryGetDateTimeOffset(out var dt)
                    ? dt : DateTimeOffset.UtcNow;

                yield return new ArtifactEntry
                {
                    RelativePath = path,
                    SizeBytes = size,
                    ContentType = contentType,
                    RunId = runId,
                    LastModified = lastModified,
                };
            }
        }
    }

    public Task SetCodeMetadataAsync(ArtifactHandle handle, CodeManifest metadata, CancellationToken ct = default)
    {
        // Manifest is managed server-side during agent execution; no client update needed.
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(ArtifactScope scope, CancellationToken ct = default)
    {
        if (scope.RunId is not null)
        {
            await _http.DeleteAsync(new Uri($"/v1/artifacts/{Uri.EscapeDataString(scope.RunId)}", UriKind.Relative), ct);
        }
    }

    public Task<Uri?> GetAccessUrlAsync(ArtifactHandle handle, string relativePath, TimeSpan ttl, CancellationToken ct = default)
    {
        var runId = handle.Scope.RunId ?? "unknown";
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        var url = $"{baseUrl}/v1/artifacts/{Uri.EscapeDataString(runId)}/{relativePath}";
        return Task.FromResult<Uri?>(new Uri(url));
    }
}
