using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for Phase 31 — Server Response Caching and ETag middleware.</summary>
public sealed class CachingTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;

    public CachingTests(SovrantWebAppFactory factory) =>
        _client = factory.CreateClient();

    private HttpRequestMessage AuthGet(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        return req;
    }

    // ── Cache-Control headers ──────────────────────────────────────────────

    [Theory]
    [InlineData("/v1/tools")]
    [InlineData("/v1/skills")]
    [InlineData("/v1/agents/templates")]
    [InlineData("/v1/config")]
    [InlineData("/v1/status")]
    public async Task CacheableRoutes_ReturnCacheControlHeader(string path)
    {
        var resp = await _client.SendAsync(AuthGet(path));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(resp.Headers.CacheControl?.Private ?? false,
            $"Expected Cache-Control: private on {path}");
        Assert.True(resp.Headers.CacheControl?.MaxAge > TimeSpan.Zero,
            $"Expected positive max-age on {path}");
    }

    [Fact]
    public async Task ChatEndpoint_DoesNotReturnCacheControl()
    {
        // POST /v1/chat/completions — must never have cache headers.
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                messages = new[] { new { role = "user", content = "hello" } },
                stream = false,
            }),
            System.Text.Encoding.UTF8,
            "application/json");

        var resp = await _client.SendAsync(req);

        // Chat should work but NOT have cache-control.
        Assert.Null(resp.Headers.CacheControl);
    }

    // ── ETag behaviour ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("/v1/tools")]
    [InlineData("/v1/skills")]
    [InlineData("/v1/agents/templates")]
    [InlineData("/v1/config")]
    [InlineData("/v1/status")]
    public async Task CacheableRoutes_ReturnETagHeader(string path)
    {
        var resp = await _client.SendAsync(AuthGet(path));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var etag = resp.Headers.ETag;
        Assert.NotNull(etag);
        Assert.False(string.IsNullOrWhiteSpace(etag.Tag));
    }

    [Theory]
    [InlineData("/v1/tools")]
    [InlineData("/v1/config")]
    public async Task ETag_MatchingIfNoneMatch_Returns304(string path)
    {
        // First request — get the ETag.
        var resp1 = await _client.SendAsync(AuthGet(path));
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        var etag = resp1.Headers.ETag!.Tag;

        // Second request — send If-None-Match with the same ETag.
        var req2 = AuthGet(path);
        req2.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        var resp2 = await _client.SendAsync(req2);

        Assert.Equal(HttpStatusCode.NotModified, resp2.StatusCode);
    }

    [Fact]
    public async Task ETag_MismatchedIfNoneMatch_Returns200()
    {
        var req = AuthGet("/v1/tools");
        req.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale-etag\""));
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Second request served from cache ───────────────────────────────────

    [Fact]
    public async Task ToolsRoute_SecondRequest_ServedFromCache()
    {
        // Two sequential requests should return identical content.
        var resp1 = await _client.SendAsync(AuthGet("/v1/tools"));
        var body1 = await resp1.Content.ReadAsStringAsync();

        var resp2 = await _client.SendAsync(AuthGet("/v1/tools"));
        var body2 = await resp2.Content.ReadAsStringAsync();

        Assert.Equal(body1, body2);
        Assert.Equal(resp1.Headers.ETag!.Tag, resp2.Headers.ETag!.Tag);
    }

    // ── Non-cacheable routes ───────────────────────────────────────────────

    [Fact]
    public async Task SessionsRoute_NoETagOnDynamicContent()
    {
        // Session list is dynamic — no ETag middleware should apply.
        var resp = await _client.SendAsync(AuthGet("/v1/sessions"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        // The ETag middleware should NOT apply to /v1/sessions.
        Assert.Null(resp.Headers.ETag);
    }

    // ── Config invalidation ────────────────────────────────────────────────

    [Fact]
    public async Task PutConfig_InvalidatesCache_NextGetReturnsFreshData()
    {
        // GET config to populate cache.
        var resp1 = await _client.SendAsync(AuthGet("/v1/config"));
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        var body1 = await resp1.Content.ReadAsStringAsync();

        // PUT config to change model.
        var putReq = new HttpRequestMessage(HttpMethod.Put, "/v1/config");
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        putReq.Content = new StringContent(
            JsonSerializer.Serialize(new { model = "test-model-changed" }),
            System.Text.Encoding.UTF8,
            "application/json");
        var putResp = await _client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // GET config again — should reflect the mutation, not stale cache.
        var resp2 = await _client.SendAsync(AuthGet("/v1/config"));
        var body2 = await resp2.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body2);
        Assert.Equal("test-model-changed", doc.RootElement.GetProperty("model").GetString());
    }
}
