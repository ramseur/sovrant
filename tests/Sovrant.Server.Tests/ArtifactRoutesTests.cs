using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 53 — integration tests for artifact routes.
/// </summary>
public sealed class ArtifactRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;

    public ArtifactRoutesTests(SovrantWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpRequestMessage Auth(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        return req;
    }

    [Fact]
    public async Task ListArtifacts_ReturnsArray()
    {
        var req = Auth(HttpMethod.Get, "/v1/artifacts");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("artifacts").ValueKind == JsonValueKind.Array);
        Assert.Equal(0, doc.RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GetArtifact_Unknown_Returns404()
    {
        var req = Auth(HttpMethod.Get, "/v1/artifacts/nonexistent-run/missing.txt");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteArtifact_ReturnsOk()
    {
        var req = Auth(HttpMethod.Delete, "/v1/artifacts/some-run-id");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("deleted").GetBoolean());
    }
}
