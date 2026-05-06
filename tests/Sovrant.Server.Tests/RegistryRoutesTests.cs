using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sovrant.Server.Tests;

/// <summary>Tests for Phase 29 registry discovery endpoints.</summary>
public sealed class RegistryRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public RegistryRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage AuthGet(string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
        return req;
    }

    // ── Tools ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTools_ReturnsListWithCount()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/tools"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        Assert.True(doc.RootElement.TryGetProperty("tools", out var tools));
        Assert.True(doc.RootElement.TryGetProperty("count", out var count));
        Assert.Equal(tools.GetArrayLength(), count.GetInt32());
        Assert.True(count.GetInt32() > 0);

        var first = tools[0];
        Assert.True(first.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task GetToolByName_ReturnsToolDefinition()
    {
        // First get the list to find a valid tool name.
        var listResp = await _client.SendAsync(AuthGet("/v1/tools"));
        var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var firstName = listDoc.RootElement.GetProperty("tools")[0].GetProperty("name").GetString()!;

        var resp = await _client.SendAsync(AuthGet($"/v1/tools/{firstName}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(firstName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetToolByName_NotFound()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/tools/nonexistent-tool-xyz"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetTools_RequiresAuth()
    {
        var resp = await _client.GetAsync("/v1/tools");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Skills ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSkills_ReturnsListWithCount()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/skills"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        Assert.True(doc.RootElement.TryGetProperty("skills", out var skills));
        Assert.True(doc.RootElement.TryGetProperty("count", out var count));
        Assert.Equal(skills.GetArrayLength(), count.GetInt32());
    }

    [Fact]
    public async Task GetSkillByName_NotFound()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/skills/nonexistent-skill-xyz"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetSkills_RequiresAuth()
    {
        var resp = await _client.GetAsync("/v1/skills");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ── Agent Templates ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAgentTemplates_ReturnsListWithCount()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/agents/templates"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        Assert.True(doc.RootElement.TryGetProperty("templates", out var templates));
        Assert.True(doc.RootElement.TryGetProperty("count", out var count));
        Assert.Equal(templates.GetArrayLength(), count.GetInt32());
        Assert.True(count.GetInt32() > 0);

        var first = templates[0];
        Assert.True(first.TryGetProperty("name", out _));
        Assert.True(first.TryGetProperty("recommended_level", out _));
    }

    [Fact]
    public async Task GetAgentTemplateByName_ReturnsDetail()
    {
        // First get the list to find a valid template name.
        var listResp = await _client.SendAsync(AuthGet("/v1/agents/templates"));
        var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var firstName = listDoc.RootElement.GetProperty("templates")[0].GetProperty("name").GetString()!;

        var resp = await _client.SendAsync(AuthGet($"/v1/agents/templates/{firstName}"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.Equal(firstName, doc.RootElement.GetProperty("name").GetString());
        Assert.True(doc.RootElement.TryGetProperty("system_prompt", out _));
        Assert.True(doc.RootElement.TryGetProperty("role", out _));
    }

    [Fact]
    public async Task GetAgentTemplateByName_NotFound()
    {
        var resp = await _client.SendAsync(AuthGet("/v1/agents/templates/nonexistent-template-xyz"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetAgentTemplates_RequiresAuth()
    {
        var resp = await _client.GetAsync("/v1/agents/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
