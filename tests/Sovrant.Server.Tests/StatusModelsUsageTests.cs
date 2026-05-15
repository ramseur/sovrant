using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for /v1/status, /v1/models, and /v1/usage endpoints.</summary>
public sealed class StatusModelsUsageTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public StatusModelsUsageTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage AuthGet(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
        return req;
    }

    // ── Status ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsProviderInfo()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/status"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("providers", out var providers));
        Assert.True(providers.GetArrayLength() > 0);
        Assert.True(doc.RootElement.TryGetProperty("active_model", out _));
        Assert.True(doc.RootElement.TryGetProperty("permission_mode", out _));
        Assert.True(doc.RootElement.TryGetProperty("active_sessions", out _));
        Assert.True(doc.RootElement.TryGetProperty("max_sessions", out _));
    }

    // ── Models ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetModels_ReturnsOpenAiCompatibleList()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/models"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.Equal("list", doc.RootElement.GetProperty("object").GetString());
        Assert.True(doc.RootElement.TryGetProperty("data", out var data));
        Assert.True(data.GetArrayLength() > 0);

        var first = data[0];
        Assert.Equal("test-provider", first.GetProperty("id").GetString());
        Assert.Equal("model", first.GetProperty("object").GetString());
        Assert.Equal("sovrant", first.GetProperty("owned_by").GetString());
    }

    // ── Usage ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsage_ReturnsTokenSummary()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/usage"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);

        Assert.True(doc.RootElement.TryGetProperty("sessions", out _));
        Assert.True(doc.RootElement.TryGetProperty("total_input_tokens", out _));
        Assert.True(doc.RootElement.TryGetProperty("total_output_tokens", out _));
        Assert.True(doc.RootElement.TryGetProperty("total_tokens", out _));
    }
}
