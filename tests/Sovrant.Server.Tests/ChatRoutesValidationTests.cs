using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for input validation on POST /v1/chat/completions.</summary>
public sealed class ChatRoutesValidationTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public ChatRoutesValidationTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage AuthPost(string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
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

}
