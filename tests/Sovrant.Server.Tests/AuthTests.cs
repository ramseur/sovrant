using System.Net;
using System.Net.Http.Headers;

namespace Sovrant.Server.Tests;

/// <summary>Tests for BearerTokenMiddleware authentication.</summary>
public sealed class AuthTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthTests(SovrantWebAppFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Health_NoAuth_Returns200()
    {
        var resp = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
    }

    [Fact]
    public async Task Health_ReportsDbStatus()
    {
        // Phase 42.5 — /health must include a db block with status,
        // schema_version, and path so monitors can detect a "running but
        // DB broken" state.
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("db", out var db));
        Assert.Equal("ok", db.GetProperty("status").GetString());
        Assert.True(db.GetProperty("schema_version").GetInt32() > 0);
    }

    [Fact]
    public async Task AuthenticatedRoute_NoToken_Returns401()
    {
        var resp = await _client.GetAsync("/v1/config");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedRoute_WrongToken_Returns401()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/config");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "wrong-token");

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedRoute_ValidToken_Returns200()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/config");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Options_NoAuth_Returns200()
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/v1/config");

        var resp = await _client.SendAsync(req);

        // OPTIONS should pass through without auth (CORS preflight).
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
