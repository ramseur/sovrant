using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 99 — PATCH /v1/sessions/{id}/privacy must enforce strict ownership.
/// Admin role gets no override; only the owner can flip the flag. Successful
/// flips write an audit row via IAuditStore.LogPrivacyChangeAsync.
/// </summary>
public sealed class PrivacyRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public PrivacyRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Patch_Anonymous_Returns401()
    {
        var sessionId = $"sess-anon-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, ownerUserId: "alice", MakeEntry("hi"));

        var resp = await _client.PatchAsJsonAsync($"/v1/sessions/{sessionId}/privacy", new { isPrivate = true });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_UnknownSession_Returns404()
    {
        var alice = await IssueAsync($"priv-alice-{Guid.NewGuid():N}", role: "user");
        var resp = await Send(HttpMethod.Patch, $"/v1/sessions/does-not-exist-{Guid.NewGuid():N}/privacy",
            alice.plaintext, JsonContent.Create(new { isPrivate = true }));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_NonOwner_Returns403()
    {
        var alice = await IssueAsync($"priv-aliceA-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"priv-bobA-{Guid.NewGuid():N}", role: "user");

        var sessionId = $"sess-nonowner-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, ownerUserId: alice.user.UserId, MakeEntry("hi"));

        var resp = await Send(HttpMethod.Patch, $"/v1/sessions/{sessionId}/privacy",
            bob.plaintext, JsonContent.Create(new { isPrivate = false }));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_AdminNonOwner_Returns403()
    {
        // Admin does NOT get an override — privacy is personal, not moderation.
        var alice = await IssueAsync($"priv-aliceB-{Guid.NewGuid():N}", role: "user");
        var admin = await IssueAsync($"priv-admin-{Guid.NewGuid():N}", role: "admin");

        var sessionId = $"sess-admin-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, ownerUserId: alice.user.UserId, MakeEntry("hi"));

        var resp = await Send(HttpMethod.Patch, $"/v1/sessions/{sessionId}/privacy",
            admin.plaintext, JsonContent.Create(new { isPrivate = false }));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_Owner_Returns204_AndAudits()
    {
        var alice = await IssueAsync($"priv-aliceC-{Guid.NewGuid():N}", role: "user");
        var sessionId = $"sess-owner-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, ownerUserId: alice.user.UserId, MakeEntry("hi"));

        var audit = (FakeAuditStore)_factory.Services.GetRequiredService<IAuditStore>();
        var beforeCount = audit.PrivacyLog.Count;

        var resp = await Send(HttpMethod.Patch, $"/v1/sessions/{sessionId}/privacy",
            alice.plaintext, JsonContent.Create(new { isPrivate = false }));

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.True(audit.PrivacyLog.Count > beforeCount);

        var lastEntry = audit.PrivacyLog[^1];
        Assert.Equal(alice.user.UserId, lastEntry.UserId);
        Assert.Equal("session", lastEntry.Kind);
        Assert.Equal(sessionId, lastEntry.Id);
        Assert.False(lastEntry.IsPrivate);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private static SessionEntry MakeEntry(string content) => new(
        Id: Guid.NewGuid().ToString("N"),
        Timestamp: DateTimeOffset.UtcNow,
        Role: "user",
        Content: content);

    private async Task<(User user, string plaintext)> IssueAsync(string username, string role)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();

        var user = await users.GetAsync(username) ?? await users.CreateAsync(userId: username, role: role);
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
