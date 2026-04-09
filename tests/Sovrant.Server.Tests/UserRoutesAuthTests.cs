using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 38 PR 3 — covers admin/self enforcement on the existing
/// <c>/v1/users/*</c> management routes.
///
/// <list type="bullet">
///   <item>Admin-only routes (POST/list, PUT, DELETE, reactivate) must
///         403 a non-admin per-user caller and 200 an admin caller.</item>
///   <item>Self-or-admin routes (GET single, sessions/usage/audit) must
///         403 a non-admin reading another user's row.</item>
///   <item>Static-token (legacy SOVRANT_TOKEN) callers must keep working
///         on every route — they are the bootstrap god-token and count as
///         admin so existing CI scripts don't break.</item>
/// </list>
/// </summary>
public sealed class UserRoutesAuthTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;
    private const string StaticToken = "test-token-123";

    public UserRoutesAuthTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AdminRoute_NonAdminPerUser_Returns403()
    {
        var alice = await IssueAsync("usr-auth-nonadmin", role: "user");

        var resp = await Send(HttpMethod.Get, "/v1/users", alice.plaintext);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_AdminPerUser_Returns200()
    {
        var admin = await IssueAsync("usr-auth-admin", role: "admin");

        var resp = await Send(HttpMethod.Get, "/v1/users", admin.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_StaticToken_Returns200()
    {
        // Legacy bootstrap god-token must still work for CI / local CLI.
        var resp = await Send(HttpMethod.Get, "/v1/users", StaticToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SelfOrAdmin_NonAdminReadingOwnRow_Returns200()
    {
        var alice = await IssueAsync("usr-self-alice", role: "user");

        var resp = await Send(HttpMethod.Get, $"/v1/users/{alice.user.UserId}", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task SelfOrAdmin_NonAdminReadingOtherRow_Returns403()
    {
        var alice = await IssueAsync("usr-cross-alice", role: "user");
        var bob = await IssueAsync("usr-cross-bob", role: "user");

        var resp = await Send(HttpMethod.Get, $"/v1/users/{bob.user.UserId}", alice.plaintext);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SelfOrAdmin_AdminReadingOtherRow_Returns200()
    {
        var admin = await IssueAsync("usr-cross-admin", role: "admin");
        var bob = await IssueAsync("usr-cross-bob-2", role: "user");

        var resp = await Send(HttpMethod.Get, $"/v1/users/{bob.user.UserId}", admin.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task PerUserDataView_NonAdminCrossUser_Returns403()
    {
        var alice = await IssueAsync("usr-data-alice", role: "user");
        var bob = await IssueAsync("usr-data-bob", role: "user");

        var resp = await Send(HttpMethod.Get, $"/v1/users/{bob.user.UserId}/usage", alice.plaintext);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task CreateUser_NonAdmin_Returns403()
    {
        var alice = await IssueAsync("usr-create-alice", role: "user");

        var body = JsonContent.Create(new { username = "should-not-be-created" });
        var resp = await Send(HttpMethod.Post, "/v1/users", alice.plaintext, body);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task<(User user, string plaintext)> IssueAsync(string username, string role)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();

        var user = await users.GetByUsernameAsync(username) ?? await users.CreateAsync(username, role: role);
        var issued = await tokens.IssueAsync(user.UserId, name: "test");
        return (user, issued.Plaintext);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, HttpContent? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = body;
        return _client.SendAsync(req);
    }
}
