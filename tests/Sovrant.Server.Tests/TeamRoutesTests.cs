using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>
/// Phase 52 — integration tests for team and run routes.
/// </summary>
public sealed class TeamRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public TeamRoutesTests(SovrantWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private HttpRequestMessage Auth(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        return req;
    }

    private async Task<JsonElement> PostJsonAsync(string path, object body)
    {
        var req = Auth(HttpMethod.Post, path);
        req.Content = JsonContent.Create(body);
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task CreateTeam_Returns201()
    {
        var req = Auth(HttpMethod.Post, "/v1/teams");
        req.Content = JsonContent.Create(new { name = "test-team", workspace_id = "ws-1", created_by = "alice" });
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.Equal("test-team", doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListTeams_ReturnsArray()
    {
        // Create a team first
        await PostJsonAsync("/v1/teams", new { name = "list-test", workspace_id = "ws-1", created_by = "alice" });

        var req = Auth(HttpMethod.Get, "/v1/teams");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("teams").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetTeam_ReturnsTeamAndMembers()
    {
        var created = await PostJsonAsync("/v1/teams", new { name = "detail-test", workspace_id = "ws-1", created_by = "alice" });
        var teamId = created.GetProperty("id").GetString()!;

        // Add a member
        await PostJsonAsync($"/v1/teams/{teamId}/members", new { name = "coder-1", role = "coder", system_prompt = "code it" });

        var req = Auth(HttpMethod.Get, $"/v1/teams/{teamId}");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.Equal("detail-test", doc.RootElement.GetProperty("team").GetProperty("name").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("members").GetArrayLength());
    }

    [Fact]
    public async Task DeleteTeam_ReturnsOk()
    {
        var created = await PostJsonAsync("/v1/teams", new { name = "delete-test", workspace_id = "ws-1", created_by = "alice" });
        var teamId = created.GetProperty("id").GetString()!;

        var req = Auth(HttpMethod.Delete, $"/v1/teams/{teamId}");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // Verify deleted
        var getReq = Auth(HttpMethod.Get, $"/v1/teams/{teamId}");
        var getResp = await _client.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task GetTeam_Unknown_Returns404()
    {
        var req = Auth(HttpMethod.Get, "/v1/teams/nonexistent");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateTeamProfile_PersistsAllFields()
    {
        var created = await PostJsonAsync("/v1/teams", new { name = "profile-persist", workspace_id = "ws-1", created_by = "alice" });
        var teamId = created.GetProperty("id").GetString()!;

        var req = Auth(HttpMethod.Put, $"/v1/teams/{teamId}/profile");
        req.Content = JsonContent.Create(new
        {
            run_mode = "sequential",
            max_concurrent = 1,
            file_locks_enabled = true,
            quality_gate_enabled = true,
            quality_gate_threshold = 9,
            decomposition_mode = "roleAware",
        });
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        // Echoed team in response reflects new profile.
        // Enum values are camelCase on the wire (route uses JsonStringEnumConverter with CamelCase).
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var team = doc.RootElement.GetProperty("team");
        Assert.Equal("sequential", team.GetProperty("run_mode").GetString());
        Assert.Equal(1, team.GetProperty("max_concurrent").GetInt32());
        Assert.True(team.GetProperty("file_locks_enabled").GetBoolean());
        Assert.True(team.GetProperty("quality_gate_enabled").GetBoolean());
        Assert.Equal(9, team.GetProperty("quality_gate_threshold").GetInt32());
        Assert.Equal("roleAware", team.GetProperty("decomposition_mode").GetString());

        // Subsequent GET returns the same persisted values.
        var getReq = Auth(HttpMethod.Get, $"/v1/teams/{teamId}");
        var getResp = await _client.SendAsync(getReq);
        getResp.EnsureSuccessStatusCode();
        var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        var persisted = getDoc.RootElement.GetProperty("team");
        Assert.Equal("sequential", persisted.GetProperty("run_mode").GetString());
        Assert.Equal(9, persisted.GetProperty("quality_gate_threshold").GetInt32());
    }

    [Fact]
    public async Task UpdateTeamProfile_PartialUpdate_LeavesOmittedFieldsUnchanged()
    {
        // Set the team to a known non-default state first.
        var created = await PostJsonAsync("/v1/teams", new { name = "profile-partial", workspace_id = "ws-1", created_by = "alice" });
        var teamId = created.GetProperty("id").GetString()!;

        var initial = Auth(HttpMethod.Put, $"/v1/teams/{teamId}/profile");
        initial.Content = JsonContent.Create(new { run_mode = "sequential", max_concurrent = 3, quality_gate_threshold = 8 });
        (await _client.SendAsync(initial)).EnsureSuccessStatusCode();

        // Partial update touches only file_locks_enabled.
        var partial = Auth(HttpMethod.Put, $"/v1/teams/{teamId}/profile");
        partial.Content = JsonContent.Create(new { file_locks_enabled = true });
        var partialResp = await _client.SendAsync(partial);
        partialResp.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await partialResp.Content.ReadAsStringAsync());
        var team = doc.RootElement.GetProperty("team");
        Assert.True(team.GetProperty("file_locks_enabled").GetBoolean());
        Assert.Equal("sequential", team.GetProperty("run_mode").GetString());
        Assert.Equal(3, team.GetProperty("max_concurrent").GetInt32());
        Assert.Equal(8, team.GetProperty("quality_gate_threshold").GetInt32());
    }

    [Fact]
    public async Task UpdateTeamProfile_Unknown_Returns404()
    {
        var req = Auth(HttpMethod.Put, "/v1/teams/nonexistent/profile");
        req.Content = JsonContent.Create(new { run_mode = "parallel" });
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task UpdateTeamProfile_InvalidRunMode_Returns400()
    {
        var created = await PostJsonAsync("/v1/teams", new { name = "profile-invalid", workspace_id = "ws-1", created_by = "alice" });
        var teamId = created.GetProperty("id").GetString()!;

        var req = Auth(HttpMethod.Put, $"/v1/teams/{teamId}/profile");
        req.Content = JsonContent.Create(new { run_mode = "turbo" });
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ListRuns_ReturnsEmptyByDefault()
    {
        var req = Auth(HttpMethod.Get, "/v1/runs");
        var resp = await _client.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("runs").ValueKind == JsonValueKind.Array);
    }

    [Fact]
    public async Task GetRun_Unknown_Returns404()
    {
        var req = Auth(HttpMethod.Get, "/v1/runs/nonexistent");
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
