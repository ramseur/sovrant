using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 38 PR 3 — covers self-service routes under <c>/v1/users/me</c>.
/// These exercise the per-user token management surface and the
/// "no <c>{id}</c> to forge" property: the target user is always the
/// caller's identity, so a non-admin cannot reach into another user's
/// tokens by guessing IDs.
/// </summary>
public sealed class MeRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public MeRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IssueToken_ReturnsPlaintextOnceAndPersists()
    {
        var bootstrap = await IssueAsync("me-issue-alice");

        var body = JsonContent.Create(new { name = "vscode" });
        var resp = await Send(HttpMethod.Post, "/v1/users/me/tokens", bootstrap.plaintext, body);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var issued = await resp.Content.ReadFromJsonAsync<IssueResponse>();
        Assert.NotNull(issued);
        Assert.StartsWith("svt_", issued!.Plaintext);
        Assert.StartsWith("tok_", issued.Token.TokenId);
        Assert.Equal(bootstrap.user.UserId, issued.Token.UserId);
        Assert.Equal("vscode", issued.Token.Name);
    }

    [Fact]
    public async Task ListTokens_ReturnsCurrentUsersTokensOnly()
    {
        var alice = await IssueAsync("me-list-alice");
        var bob = await IssueAsync("me-list-bob");

        // Alice issues a 2nd token via the API to confirm both show up.
        await Send(HttpMethod.Post, "/v1/users/me/tokens", alice.plaintext,
            JsonContent.Create(new { name = "second" }));

        var resp = await Send(HttpMethod.Get, "/v1/users/me/tokens", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var listed = await resp.Content.ReadFromJsonAsync<ListResponse>();
        Assert.NotNull(listed);
        Assert.True(listed!.Tokens.Count >= 2);
        Assert.All(listed.Tokens, t => Assert.Equal(alice.user.UserId, t.UserId));
        Assert.DoesNotContain(listed.Tokens, t => t.UserId == bob.user.UserId);
    }

    [Fact]
    public async Task RevokeToken_OwnedByCaller_Succeeds()
    {
        var alice = await IssueAsync("me-revoke-alice");
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var owned = await tokens.ListAsync(alice.user.UserId);
        var target = owned[0].TokenId;

        var resp = await Send(HttpMethod.Delete, $"/v1/users/me/tokens/{target}", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // The just-revoked token should now fail to authenticate.
        var unauth = await Send(HttpMethod.Get, "/v1/users/me", alice.plaintext);
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);
    }

    [Fact]
    public async Task RevokeToken_NotOwned_Returns404()
    {
        var alice = await IssueAsync("me-cross-alice");
        var bob = await IssueAsync("me-cross-bob");

        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var bobsTokens = await tokens.ListAsync(bob.user.UserId);
        var bobsTokenId = bobsTokens[0].TokenId;

        // Alice tries to revoke Bob's token by guessing the id.
        var resp = await Send(HttpMethod.Delete, $"/v1/users/me/tokens/{bobsTokenId}", alice.plaintext);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // Bob's token still works — proving Alice did not actually revoke it.
        var stillWorks = await Send(HttpMethod.Get, "/v1/users/me", bob.plaintext);
        Assert.Equal(HttpStatusCode.OK, stillWorks.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsCallerProfile()
    {
        var alice = await IssueAsync("me-profile-alice");
        var resp = await Send(HttpMethod.Get, "/v1/users/me", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var profile = await resp.Content.ReadFromJsonAsync<ProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(alice.user.UserId, profile!.UserId);
        Assert.Equal("me-profile-alice", profile.Username);
    }

    [Fact]
    public async Task MeRoutes_RejectStaticToken_With400()
    {
        // The legacy static token has no associated user, so /me is meaningless.
        var resp = await Send(HttpMethod.Get, "/v1/users/me", "test-token-123");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private async Task<(User user, string plaintext)> IssueAsync(string username)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();

        var user = await users.GetByUsernameAsync(username) ?? await users.CreateAsync(username);
        var issued = await tokens.IssueAsync(user.UserId, name: "bootstrap");
        return (user, issued.Plaintext);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, HttpContent? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = body;
        return _client.SendAsync(req);
    }

    private sealed record TokenDto
    {
        [JsonPropertyName("token_id")] public string TokenId { get; init; } = "";
        [JsonPropertyName("user_id")] public string UserId { get; init; } = "";
        [JsonPropertyName("name")] public string? Name { get; init; }
    }

    private sealed record IssueResponse
    {
        [JsonPropertyName("token")] public TokenDto Token { get; init; } = new();
        [JsonPropertyName("plaintext")] public string Plaintext { get; init; } = "";
    }

    private sealed record ListResponse
    {
        [JsonPropertyName("tokens")] public IReadOnlyList<TokenDto> Tokens { get; init; } = [];
        [JsonPropertyName("count")] public int Count { get; init; }
    }

    private sealed record ProfileDto
    {
        [JsonPropertyName("user_id")] public string UserId { get; init; } = "";
        [JsonPropertyName("username")] public string Username { get; init; } = "";
    }
}
