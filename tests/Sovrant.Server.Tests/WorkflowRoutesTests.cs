using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Server.Tests;

/// <summary>
/// Integration tests for the Phase 51 workflow routes. These call through
/// the real SqliteWorkflowStore and default planner/executor/gate wired
/// into the WebApplicationFactory, exercising the full HTTP surface with
/// the same code paths a CLI would hit.
/// </summary>
public sealed class WorkflowRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public WorkflowRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage Auth(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _factory.TestAdminToken);
        return req;
    }

    [Fact]
    public async Task Create_Then_Get_Returns201_And_Workflow()
    {
        var create = Auth(HttpMethod.Post, "/v1/workflows");
        create.Content = JsonContent.Create(new { goal = "ship the refactor", ownerUserId = "alice" });
        var createResp = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var doc = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var workflowId = doc.GetProperty("id").GetString()!;
        Assert.StartsWith("workflow-", workflowId, StringComparison.Ordinal);

        var get = Auth(HttpMethod.Get, $"/v1/workflows/{workflowId}");
        var getResp = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var fetched = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ship the refactor", fetched.GetProperty("goal").GetString());
    }

    [Fact]
    public async Task Create_MissingGoal_Returns400()
    {
        var req = Auth(HttpMethod.Post, "/v1/workflows");
        req.Content = JsonContent.Create(new { goal = "" });
        var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/workflows/workflow-nope"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByOwnerAndStatus()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            await store.CreateAsync("list goal A", ownerUserId: "routes-user");
            await store.CreateAsync("list goal B", ownerUserId: "routes-user");
        }

        var resp = await _client.SendAsync(Auth(HttpMethod.Get,
            "/v1/workflows?ownerUserId=routes-user&status=planning"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var workflows = doc.GetProperty("workflows");
        Assert.True(workflows.GetArrayLength() >= 2);
    }

    [Fact]
    public async Task Events_ReturnsJournalInsertionOrder()
    {
        string workflowId;
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IWorkflowStore>();
            var m = await store.CreateAsync("journal test");
            workflowId = m.Id;
            await store.AppendEventAsync(workflowId, WorkflowEventTypes.RunStarted, "{}");
            await store.AppendEventAsync(workflowId, WorkflowEventTypes.RunCompleted, "{}");
        }

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, $"/v1/workflows/{workflowId}/events"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var events = doc.GetProperty("events");
        Assert.Equal(3, events.GetArrayLength());
        Assert.Equal(WorkflowEventTypes.WorkflowCreated, events[0].GetProperty("event_type").GetString());
        Assert.Equal(WorkflowEventTypes.RunStarted, events[1].GetProperty("event_type").GetString());
        Assert.Equal(WorkflowEventTypes.RunCompleted, events[2].GetProperty("event_type").GetString());
    }
}
