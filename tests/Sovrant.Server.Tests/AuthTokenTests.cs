using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 38 PR 2 — covers per-user (<c>svt_*</c>) bearer authentication via
/// <see cref="ITokenService"/>. Asserts both the status-code outcome AND the
/// identity attached to the request via <c>/v1/whoami</c>, so a buggy
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

        var who = await CallWhoAmI(plaintext);

        Assert.NotNull(who);
        Assert.Equal("token", who!.AuthMode);
        Assert.Equal(user.UserId, who.UserId);
        Assert.NotNull(who.TokenId);
        Assert.StartsWith("tok_", who.TokenId);
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

        var resp = await SendWithToken("/v1/whoami", plaintext);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task TokenForInactiveUser_Returns401()
    {
        var (user, plaintext) = await IssueAsync("auth-token-inactive");
        var users = _factory.Services.GetRequiredService<IUserService>();
        await users.DeactivateAsync(user.UserId);

        var resp = await SendWithToken("/v1/whoami", plaintext);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task UnknownPerUserToken_Returns401()
    {
        var resp = await SendWithToken("/v1/whoami", "svt_completely-unknown-token");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task StaticToken_StillWorks_ButHasNoUserId()
    {
        // The legacy SOVRANT_TOKEN path: factory sets it to "test-token-123".
        var who = await CallWhoAmI("test-token-123");

        Assert.NotNull(who);
        Assert.Equal("static", who!.AuthMode);
        Assert.Null(who.UserId);
        Assert.Null(who.TokenId);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task<(User user, string plaintext)> IssueAsync(string username)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();

        var user = await users.GetByUsernameAsync(username) ?? await users.CreateAsync(username);
        var issued = await tokens.IssueAsync(user.UserId, name: "test");
        return (user, issued.Plaintext);
    }

    private Task<HttpResponseMessage> SendWithToken(string path, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(req);
    }

    private async Task<WhoAmIDto?> CallWhoAmI(string token)
    {
        var resp = await SendWithToken("/v1/whoami", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Content.ReadFromJsonAsync<WhoAmIDto>();
    }

    private sealed record WhoAmIDto
    {
        [JsonPropertyName("auth_mode")] public string AuthMode { get; init; } = "";
        [JsonPropertyName("user_id")] public string? UserId { get; init; }
        [JsonPropertyName("token_id")] public string? TokenId { get; init; }
    }
}
