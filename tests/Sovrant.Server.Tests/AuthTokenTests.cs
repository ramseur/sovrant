using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 38 PR 2/3 — covers per-user (<c>svt_*</c>) bearer authentication via
/// <see cref="ITokenService"/>. Asserts both the status-code outcome AND the
/// identity attached to the request via <c>GET /v1/users/me</c>, so a buggy
/// middleware that lets requests through without setting <c>user_id</c>
/// would still be caught.
/// </summary>
public sealed class AuthTokenTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthTokenTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PerUserToken_ResolvesToUserIdentity()
    {
        var (user, plaintext) = await IssueAsync("auth-token-alice");

        var profile = await GetMe(plaintext);

        Assert.NotNull(profile);
        Assert.Equal(user.UserId, profile!.UserId);
        Assert.Equal("auth-token-alice", profile.Username);
    }

    [Fact]
    public async Task RevokedToken_Returns401()
    {
        var (_, plaintext) = await IssueAsync("auth-token-revoked");
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var users = _factory.Services.GetRequiredService<IUserService>();
        var u = await users.GetByUsernameAsync("auth-token-revoked");
        var listed = await tokens.ListAsync(u!.UserId);
        await tokens.RevokeAsync(listed[0].TokenId);

        var resp = await SendWithToken(HttpMethod.Get, "/v1/users/me", plaintext);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task TokenForInactiveUser_Returns401()
    {
        var (user, plaintext) = await IssueAsync("auth-token-inactive");
        var users = _factory.Services.GetRequiredService<IUserService>();
        await users.DeactivateAsync(user.UserId);

        var resp = await SendWithToken(HttpMethod.Get, "/v1/users/me", plaintext);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UnknownPerUserToken_Returns401()
    {
        var resp = await SendWithToken(HttpMethod.Get, "/v1/users/me", "svt_completely-unknown-token");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    internal async Task<(User user, string plaintext)> IssueAsync(string username, string role = "user")
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();

        var user = await users.GetByUsernameAsync(username) ?? await users.CreateAsync(username, role: role);
        var issued = await tokens.IssueAsync(user.UserId, name: "test");
        return (user, issued.Plaintext);
    }

    internal Task<HttpResponseMessage> SendWithToken(HttpMethod method, string path, string token, HttpContent? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = body;
        return _client.SendAsync(req);
    }

    private async Task<UserProfileDto?> GetMe(string token)
    {
        var resp = await SendWithToken(HttpMethod.Get, "/v1/users/me", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<UserProfileDto>();
    }

    internal sealed record UserProfileDto
    {
        [JsonPropertyName("user_id")] public string UserId { get; init; } = "";
        [JsonPropertyName("username")] public string Username { get; init; } = "";
        [JsonPropertyName("role")] public string? Role { get; init; }
    }
}
