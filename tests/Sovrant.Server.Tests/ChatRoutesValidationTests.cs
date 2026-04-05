using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for input validation on POST /v1/chat/completions.</summary>
public sealed class ChatRoutesValidationTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;

    public ChatRoutesValidationTests(SovrantWebAppFactory factory) =>
        _client = factory.CreateClient();

    private HttpRequestMessage AuthPost(string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        return req;
    }

    [Fact]
    public async Task ChatCompletions_InvalidSessionId_Returns400()
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            session_id = "../etc/passwd",
            messages = new[] { new { role = "user", content = "hello" } },
        });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid session_id", body);
    }

    [Fact]
    public async Task ChatCompletions_InvalidModelName_Returns400()
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            model = "model; DROP TABLE users;",
            messages = new[] { new { role = "user", content = "hello" } },
        });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid model name", body);
    }

    [Fact]
    public async Task ChatCompletions_NoUserMessage_Returns400()
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            messages = new[] { new { role = "system", content = "You are helpful." } },
        });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("No user message", body);
    }

    [Fact]
    public async Task ChatCompletions_EmptyMessages_Returns400()
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            messages = Array.Empty<object>(),
        });

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── SSRF Protection ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://localhost:8080/v1")]
    [InlineData("http://127.0.0.1/v1")]
    [InlineData("http://10.0.0.1/v1")]
    [InlineData("http://192.168.1.1/v1")]
    [InlineData("http://169.254.169.254/v1")]
    [InlineData("http://172.16.0.1/v1")]
    public async Task ChatCompletions_SsrfReservedAddress_Returns400(string baseUrl)
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "hello" } },
        });
        req.Headers.Add("X-LLM-Api-Key", "some-key");
        req.Headers.Add("X-LLM-Base-Url", baseUrl);

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("reserved", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChatCompletions_SsrfInvalidScheme_Returns400()
    {
        var req = AuthPost("/v1/chat/completions", new
        {
            messages = new[] { new { role = "user", content = "hello" } },
        });
        req.Headers.Add("X-LLM-Api-Key", "some-key");
        req.Headers.Add("X-LLM-Base-Url", "ftp://evil.com/v1");

        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid", body);
    }
}
