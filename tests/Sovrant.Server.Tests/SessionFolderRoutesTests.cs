using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Users;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 133 — <c>/v1/session-folders</c> and <c>PUT /v1/sessions/{id}/folder</c>.
/// The folder store is the real SQLite one (the test factory isolates the DB);
/// conversations that need a real <c>sessions</c> row are inserted directly.
/// </summary>
public sealed class SessionFolderRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public SessionFolderRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_List_Rename_Move_Delete_RoundTrip()
    {
        var (user, token) = await IssueAsync("user");

        var client = await CreateAsync(token, "Client A");
        var proposals = await CreateAsync(token, "Proposals", client);

        var list = await JsonAsync(await Send(HttpMethod.Get, "/v1/session-folders", token));
        var folders = list.GetProperty("folders").EnumerateArray().ToList();
        Assert.Equal(2, folders.Count);
        Assert.Contains(folders, f => f.GetProperty("folder_id").GetString() == proposals
                                   && f.GetProperty("parent_folder_id").GetString() == client);

        var renamed = await Send(HttpMethod.Patch, $"/v1/session-folders/{proposals}", token, Json(new { name = "Bids" }));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Bids", (await JsonAsync(renamed)).GetProperty("name").GetString());

        // parent_folder_id present and null = move to the top level.
        var moved = await Send(HttpMethod.Patch, $"/v1/session-folders/{proposals}", token,
            new StringContent("""{"parent_folder_id":null}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await JsonAsync(moved)).GetProperty("parent_folder_id").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await Send(HttpMethod.Delete, $"/v1/session-folders/{proposals}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Delete, $"/v1/session-folders/{proposals}", token)).StatusCode);
        _ = user;
    }

    [Fact]
    public async Task RuleRefusals_MapToStatusCodes()
    {
        var (_, token) = await IssueAsync("user");
        var parent = await CreateAsync(token, "Parent");
        var child = await CreateAsync(token, "Child", parent);

        var dup = await Send(HttpMethod.Post, "/v1/session-folders", token, Json(new { name = "parent" }));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal("duplicate_name", (await JsonAsync(dup)).GetProperty("code").GetString());

        var cycle = await Send(HttpMethod.Patch, $"/v1/session-folders/{parent}", token, Json(new { parent_folder_id = child }));
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        Assert.Equal("cycle", (await JsonAsync(cycle)).GetProperty("code").GetString());

        var empty = await Send(HttpMethod.Post, "/v1/session-folders", token, Json(new { name = "   " }));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("invalid_name", (await JsonAsync(empty)).GetProperty("code").GetString());

        string? deepest = null;
        for (var level = 1; level <= SessionFolderRules.MaxDepth; level++)
            deepest = await CreateAsync(token, $"Level {level}", deepest);
        var tooDeep = await Send(HttpMethod.Post, "/v1/session-folders", token, Json(new { name = "Level 6", parent_folder_id = deepest }));
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
        Assert.Equal("too_deep", (await JsonAsync(tooDeep)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Folders_ArePrivateToTheirOwner()
    {
        var (_, alice) = await IssueAsync("user");
        var (_, bob) = await IssueAsync("user");
        var bobs = await CreateAsync(bob, "Bob's");

        var aliceList = await JsonAsync(await Send(HttpMethod.Get, "/v1/session-folders", alice));
        Assert.DoesNotContain(aliceList.GetProperty("folders").EnumerateArray(), f => f.GetProperty("folder_id").GetString() == bobs);

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(HttpMethod.Patch, $"/v1/session-folders/{bobs}", alice, Json(new { name = "Mine now" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Delete, $"/v1/session-folders/{bobs}", alice)).StatusCode);
    }

    [Fact]
    public async Task PutSessionFolder_FilesOwnConversation_AndRefusesOthers()
    {
        var (alice, aliceToken) = await IssueAsync("user");
        var (bob, bobToken) = await IssueAsync("user");
        var aliceChat = $"chat-{Guid.NewGuid():N}";
        var bobChat = $"chat-{Guid.NewGuid():N}";
        InsertSessionRow(aliceChat, alice.UserId);
        InsertSessionRow(bobChat, bob.UserId);
        var folder = await CreateAsync(aliceToken, "Research");
        var bobsFolder = await CreateAsync(bobToken, "Bob's");

        var filed = await Send(HttpMethod.Put, $"/v1/sessions/{aliceChat}/folder", aliceToken, Json(new { folder_id = folder }));
        Assert.Equal(HttpStatusCode.NoContent, filed.StatusCode);
        Assert.Equal(folder, FolderOf(aliceChat));

        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(HttpMethod.Put, $"/v1/sessions/{bobChat}/folder", aliceToken, Json(new { folder_id = folder }))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Send(HttpMethod.Put, $"/v1/sessions/{aliceChat}/folder", aliceToken, Json(new { folder_id = bobsFolder }))).StatusCode);
        Assert.Null(FolderOf(bobChat));

        var unfiled = await Send(HttpMethod.Put, $"/v1/sessions/{aliceChat}/folder", aliceToken,
            new StringContent("""{"folder_id":null}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, unfiled.StatusCode);
        Assert.Null(FolderOf(aliceChat));
    }

    [Fact]
    public async Task ListSessions_IncludesFolderTitleAndLabels_AndHidesSystemSessions()
    {
        var (user, token) = await IssueAsync("user");
        var chat = $"webhook:slack:{Guid.NewGuid():N}";
        _factory.SessionStore.Seed(chat, user.UserId, Entry("hi"));
        _factory.SessionStore.Seed("__sovrant_mission_planner__", user.UserId, Entry("internal"));

        var doc = await JsonAsync(await Send(HttpMethod.Get, "/v1/sessions", token));
        var rows = doc.GetProperty("sessions").EnumerateArray().ToList();

        var row = rows.Single(r => r.GetProperty("session_id").GetString() == chat);
        Assert.Equal(chat, row.GetProperty("id").GetString()); // the original field is kept
        Assert.True(row.TryGetProperty("folder_id", out _));
        Assert.Equal("Webhook · slack", row.GetProperty("labels")[0].GetProperty("text").GetString());
        Assert.DoesNotContain(rows, r => r.GetProperty("session_id").GetString() == "__sovrant_mission_planner__");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> CreateAsync(string token, string name, string? parent = null)
    {
        var resp = await Send(HttpMethod.Post, "/v1/session-folders", token, Json(new { name, parent_folder_id = parent }));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await JsonAsync(resp)).GetProperty("folder_id").GetString()!;
    }

    private SqliteConnection Open()
    {
        var path = _factory.Services.GetRequiredService<SqliteStorageProvider>().DatabasePath!;
        var conn = new SqliteConnection($"Data Source={path};Cache=Shared");
        conn.Open();
        return conn;
    }

    private void InsertSessionRow(string sessionId, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sessions (session_id, user_id) VALUES ($sid, $uid)";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.ExecuteNonQuery();
    }

    private string? FolderOf(string sessionId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT folder_id FROM sessions WHERE session_id = $sid";
        cmd.Parameters.AddWithValue("$sid", sessionId);
        return cmd.ExecuteScalar() as string;
    }

    private static SessionEntry Entry(string content) => new(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, "user", content);

    private static JsonContent Json(object body) => JsonContent.Create(body);

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage resp)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.Clone();
    }

    private async Task<(User user, string plaintext)> IssueAsync(string role)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var id = $"folders-{Guid.NewGuid():N}@example.com";
        var user = await users.CreateAsync(userId: id, role: role);
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
