using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 38 — cross-user isolation on <c>/v1/sessions</c>, <c>/v1/sessions/{id}</c>,
/// <c>/v1/usage</c>, and <c>/v1/sessions/{id}/export</c>.
///
/// <para>These tests close the gap where any authenticated caller could read,
/// delete, or summarise any session in the store regardless of owner. Admin
/// callers (legacy static token and <c>users.role = 'admin'</c>) retain the
/// full view.</para>
/// </summary>
public sealed class SessionOwnershipTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public SessionOwnershipTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListSessions_NonAdmin_SeesOnlyOwnSessions()
    {
        var alice = await IssueAsync($"sess-list-alice-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"sess-list-bob-{Guid.NewGuid():N}", role: "user");

        var aliceSession = $"sess-alice-{Guid.NewGuid():N}";
        var bobSession = $"sess-bob-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(aliceSession, alice.user.UserId, MakeEntry("hi"));
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("hello"));

        var resp = await Send(HttpMethod.Get, "/v1/sessions", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("sessions").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString())
            .ToList();

        Assert.Contains(aliceSession, ids);
        Assert.DoesNotContain(bobSession, ids);
    }

    [Fact]
    public async Task ListSessions_Admin_SeesAllSessions()
    {
        var admin = await IssueAsync($"sess-list-admin-{Guid.NewGuid():N}", role: "admin");
        var bob = await IssueAsync($"sess-list-bob2-{Guid.NewGuid():N}", role: "user");

        var bobSession = $"sess-admin-view-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("bob message"));

        var resp = await Send(HttpMethod.Get, "/v1/sessions", admin.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("sessions").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString())
            .ToList();

        Assert.Contains(bobSession, ids);
    }

    [Fact]
    public async Task ListSessions_AdminToken_SeesAllSessions()
    {
        var admin = await IssueAsync($"sess-admin-{Guid.NewGuid():N}", role: "admin");
        var bob = await IssueAsync($"sess-admin-bob-{Guid.NewGuid():N}", role: "user");
        var bobSession = $"sess-admin-view-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("bob"));

        var resp = await Send(HttpMethod.Get, "/v1/sessions", admin.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("sessions").EnumerateArray()
            .Select(e => e.GetProperty("id").GetString())
            .ToList();

        Assert.Contains(bobSession, ids);
    }

    [Fact]
    public async Task GetSession_NonAdminReadingOtherUsersSession_Returns404()
    {
        var alice = await IssueAsync($"sess-get-alice-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"sess-get-bob-{Guid.NewGuid():N}", role: "user");

        var bobSession = $"sess-get-bob-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("secret"));

        var resp = await Send(HttpMethod.Get, $"/v1/sessions/{bobSession}", alice.plaintext);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetSession_Owner_Returns200()
    {
        var alice = await IssueAsync($"sess-own-alice-{Guid.NewGuid():N}", role: "user");
        var sessionId = $"sess-own-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, alice.user.UserId, MakeEntry("mine"));

        var resp = await Send(HttpMethod.Get, $"/v1/sessions/{sessionId}", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteSession_NonAdminOnOthersSession_Returns404_AndKeepsData()
    {
        var alice = await IssueAsync($"sess-del-alice-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"sess-del-bob-{Guid.NewGuid():N}", role: "user");

        var bobSession = $"sess-del-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("keep me"));

        var resp = await Send(HttpMethod.Delete, $"/v1/sessions/{bobSession}", alice.plaintext);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // Bob's data must still be there — alice's rejected delete was a no-op.
        var stillThere = await _factory.SessionStore.GetOwnerAsync(bobSession);
        Assert.Equal(bob.user.UserId, stillThere);
    }

    [Fact]
    public async Task DeleteSession_Owner_Returns200()
    {
        var alice = await IssueAsync($"sess-delok-alice-{Guid.NewGuid():N}", role: "user");
        var sessionId = $"sess-delok-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(sessionId, alice.user.UserId, MakeEntry("bye"));

        var resp = await Send(HttpMethod.Delete, $"/v1/sessions/{sessionId}", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Null(await _factory.SessionStore.GetOwnerAsync(sessionId));
    }

    [Fact]
    public async Task ExportSession_NonAdminCrossUser_Returns404()
    {
        var alice = await IssueAsync($"sess-exp-alice-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"sess-exp-bob-{Guid.NewGuid():N}", role: "user");

        var bobSession = $"sess-exp-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("private"));

        var resp = await Send(HttpMethod.Get, $"/v1/sessions/{bobSession}/export", alice.plaintext);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Usage_NonAdmin_OnlyShowsOwnSessions()
    {
        var alice = await IssueAsync($"usg-alice-{Guid.NewGuid():N}", role: "user");
        var bob = await IssueAsync($"usg-bob-{Guid.NewGuid():N}", role: "user");

        var aliceSession = $"usg-alice-{Guid.NewGuid():N}";
        var bobSession = $"usg-bob-{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(aliceSession, alice.user.UserId, MakeEntry("a"));
        _factory.SessionStore.Seed(bobSession, bob.user.UserId, MakeEntry("b"));

        var resp = await Send(HttpMethod.Get, "/v1/usage", alice.plaintext);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("sessions").EnumerateArray()
            .Select(e => e.GetProperty("session_id").GetString())
            .ToList();

        Assert.Contains(aliceSession, ids);
        Assert.DoesNotContain(bobSession, ids);
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
