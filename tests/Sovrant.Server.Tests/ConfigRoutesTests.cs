using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for GET/PUT /v1/config.</summary>
public sealed class ConfigRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public ConfigRoutesTests(SovrantWebAppFactory factory)
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

    private HttpRequestMessage AuthPut(string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
        return req;
    }

    [Fact]
    public async Task GetConfig_ReturnsCurrentConfig()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/config"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("model", out _));
        Assert.True(doc.RootElement.TryGetProperty("base_url", out _));
        Assert.True(doc.RootElement.TryGetProperty("permission_mode", out _));
    }

    [Fact]
    public async Task PutConfig_ValidModel_UpdatesAndReturns()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { model = "gpt-4o-mini" }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"model\":\"gpt-4o-mini\"", body);
    }

    [Fact]
    public async Task PutConfig_InvalidModel_Returns400()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { model = "invalid model name!" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid model name", body);
    }

    [Fact]
    public async Task PutConfig_InvalidBaseUrl_Returns400()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { base_url = "not-a-url" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid base_url", body);
    }

    [Fact]
    public async Task PutConfig_ValidBaseUrl_Succeeds()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { base_url = "https://api.openai.com/v1" }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PutConfig_FtpBaseUrl_Returns400()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { base_url = "ftp://evil.com/v1" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PutConfig_UnknownProvider_Returns400()
    {
        var resp = await _client.SendAsync(AuthPut("/v1/config", new { provider = "nonexistent-provider" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
