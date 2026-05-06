using System.Net;
using System.Net.Http.Headers;
using Sovrant.Runtime.Session;

namespace Sovrant.Server.Tests;

/// <summary>Tests for session management endpoints.</summary>
public sealed class SessionRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public SessionRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage Auth(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
        return req;
    }

    [Fact]
    public async Task ListSessions_ReturnsSessionList()
    {
        _factory.SessionStore.Seed("sess-1",
            new SessionEntry("1", DateTimeOffset.UtcNow, "user", "hello"));

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("sess-1", body);
    }

    [Fact]
    public async Task GetSession_ValidId_ReturnsMessages()
    {
        _factory.SessionStore.Seed("test-get",
            new SessionEntry("1", DateTimeOffset.UtcNow, "user", "What is 2+2?"),
            new SessionEntry("2", DateTimeOffset.UtcNow, "assistant", "4"));

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/test-get"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("\"session_id\":\"test-get\"", body);
        Assert.Contains("What is 2+2?", body);
    }

    [Fact]
    public async Task GetSession_NotFound_Returns404()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/nonexistent-session"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetSession_InvalidId_Returns400()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/invalid%20session%20id"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid session ID", body);
    }

    [Fact]
    public async Task GetSession_PathTraversalId_Returns400()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/..%2F..%2Fetc%2Fpasswd"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ExportSession_ValidId_ReturnsMarkdown()
    {
        _factory.SessionStore.Seed("export-test",
            new SessionEntry("1", DateTimeOffset.UtcNow, "user", "Hello"),
            new SessionEntry("2", DateTimeOffset.UtcNow, "assistant", "Hi there"));

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/export-test/export"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("# Session: export-test", body);
        Assert.Contains("## User", body);
        Assert.Contains("## Assistant", body);
    }

    [Fact]
    public async Task ExportSession_InvalidId_Returns400()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/sessions/bad session/export"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
