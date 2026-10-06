using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>2.0 — GET /v1/mcp/servers reports Phase 139's connection status; retry is admin-only.</summary>
public sealed class McpServerStatusRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public McpServerStatusRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Unavailable_Server_Reports_State_Message_And_Retry()
    {
        var name = $"status-{Guid.NewGuid():N}";
        await _factory.Services.GetRequiredService<IMcpServerStore>()
            .UpsertAsync(name, new McpServerConfig { Url = new Uri("https://does-not-exist.invalid/mcp") });
        var failure = McpConnectionError.Classify(name, new McpServerConfig { Url = new Uri("https://does-not-exist.invalid/mcp") },
            new InvalidOperationException("No such host is known."));
        _factory.Services.GetRequiredService<McpServerStatusRegistry>().Set(
            new McpServerStatus(name, McpServerState.Unavailable, failure, NextRetryAt: DateTimeOffset.UtcNow.AddMinutes(1), MaxAttempts: 3));

        var resp = await Send(HttpMethod.Get, "/v1/mcp/servers", _factory.TestAdminToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var server = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("servers")
            .EnumerateArray().Single(s => s.GetProperty("name").GetString() == name);

        Assert.False(server.GetProperty("connected").GetBoolean());
        Assert.Equal("unavailable", server.GetProperty("state").GetString());
        Assert.Equal("dns", server.GetProperty("kind").GetString());
        Assert.True(server.GetProperty("retrying").GetBoolean());
        Assert.StartsWith("Couldn't reach does-not-exist.invalid.", server.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retry_Is_Admin_Only_And_404s_For_Unknown_Servers()
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var member = await users.CreateAsync(userId: $"mcp-member-{Guid.NewGuid():N}", role: "user");
        var memberToken = (await tokens.IssueAsync(member.UserId, name: "test")).Plaintext;

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/v1/mcp/servers/anything/retry", memberToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Post, $"/v1/mcp/servers/nope-{Guid.NewGuid():N}/retry", _factory.TestAdminToken)).StatusCode);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }
}
