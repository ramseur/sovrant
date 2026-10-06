using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workflows;

namespace Sovrant.Server.Tests;

/// <summary>
/// 2.0 API parity — the workflow features the apps already had (plan first, edit the plan,
/// cancel) are now on the HTTP API, and create binds the SDK's snake_case fields.
/// </summary>
public sealed class WorkflowPlanRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly SovrantWebAppFactory _factory;
    private readonly HttpClient _client;

    public WorkflowPlanRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_Binds_Snake_Case_Fields_From_The_SDK()
    {
        var resp = await Send(HttpMethod.Post, "/v1/workflows", _factory.TestAdminToken,
            JsonContent.Create(new { goal = "bind test", session_id = "sess-bind-1", workspace_id = "ws-bind", project_id = "proj-bind" }));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var wf = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sess-bind-1", wf.GetProperty("session_id").GetString());
        Assert.Equal("ws-bind", wf.GetProperty("workspace_id").GetString());
        Assert.Equal("proj-bind", wf.GetProperty("project_id").GetString());
    }

    [Fact]
    public async Task Plan_Generates_A_Plan_And_Waits_For_Review()
    {
        // The real planner calls a model; swap in the deterministic one-step planner.
        using var app = _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<IWorkflowPlanner, SimpleWorkflowPlanner>()));
        using var client = app.CreateClient();
        // That's a separate app instance with its own stores: issue a token there.
        var users = app.Services.GetRequiredService<IUserService>();
        var tokens = app.Services.GetRequiredService<ITokenService>();
        var planner = await users.CreateAsync(userId: $"wfp-planner-{Guid.NewGuid():N}", role: "user");
        var token = (await tokens.IssueAsync(planner.UserId, name: "test")).Plaintext;

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/workflows/plan")
        {
            Content = JsonContent.Create(new { goal = "write the release notes" }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var wf = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("awaitingHuman", wf.GetProperty("status").GetString());
        Assert.Contains("write the release notes", wf.GetProperty("plan_json").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_Plan_Replaces_The_Steps_Then_Refuses_Empty_Plans()
    {
        var id = await CreateAsync(_factory.TestAdminToken, "plan edit");

        var saved = await Send(HttpMethod.Put, $"/v1/workflows/{id}/plan", _factory.TestAdminToken, JsonContent.Create(new
        {
            steps = new object[]
            {
                new { intent = "draft the outline", expected_outcome = "an outline exists", tier = "fast" },
                new { intent = "write it up" },
            },
        }));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var wf = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var plan = wf.GetProperty("plan_json").GetString()!;
        Assert.Contains("draft the outline", plan, StringComparison.Ordinal);
        Assert.Contains("write it up", plan, StringComparison.Ordinal);
        Assert.Equal("awaitingHuman", wf.GetProperty("status").GetString());

        var empty = await Send(HttpMethod.Put, $"/v1/workflows/{id}/plan", _factory.TestAdminToken,
            JsonContent.Create(new { steps = new[] { new { intent = "  " } } }));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Cancel_Cancels_Once_Then_Conflicts()
    {
        var id = await CreateAsync(_factory.TestAdminToken, "cancel me");

        var first = await Send(HttpMethod.Post, $"/v1/workflows/{id}/cancel", _factory.TestAdminToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("cancelled", (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var second = await Send(HttpMethod.Post, $"/v1/workflows/{id}/cancel", _factory.TestAdminToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var store = _factory.Services.GetRequiredService<IWorkflowStore>();
        var events = await store.GetEventsAsync(id);
        Assert.Single(events, e => e.EventType == WorkflowEventTypes.Cancelled);
    }

    [Fact]
    public async Task Only_The_Owner_Or_An_Admin_Can_Edit_Or_Cancel()
    {
        var alice = await IssueAsync($"wfp-alice-{Guid.NewGuid():N}");
        var bob = await IssueAsync($"wfp-bob-{Guid.NewGuid():N}");
        var id = await CreateAsync(alice, "alice's workflow");

        var edit = await Send(HttpMethod.Put, $"/v1/workflows/{id}/plan", bob,
            JsonContent.Create(new { steps = new[] { new { intent = "hijack" } } }));
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        var cancel = await Send(HttpMethod.Post, $"/v1/workflows/{id}/cancel", bob);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);

        var own = await Send(HttpMethod.Post, $"/v1/workflows/{id}/cancel", alice);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Unknown_Workflow_Is_404_For_Plan_And_Cancel()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Post, "/v1/workflows/workflow-nope/cancel", _factory.TestAdminToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Put, "/v1/workflows/workflow-nope/plan", _factory.TestAdminToken,
            JsonContent.Create(new { steps = new[] { new { intent = "x" } } }))).StatusCode);
    }

    private async Task<string> CreateAsync(string token, string goal)
    {
        var resp = await Send(HttpMethod.Post, "/v1/workflows", token, JsonContent.Create(new { goal }));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        // Park it where the background scheduler won't pick it up (it only advances Planning/Running),
        // so a scheduler tick can't run or fail it mid-test.
        await _factory.Services.GetRequiredService<IWorkflowStore>().UpdateStateAsync(id, WorkflowStatus.AwaitingHuman);
        return id;
    }

    private async Task<string> IssueAsync(string userId)
    {
        var users = _factory.Services.GetRequiredService<IUserService>();
        var tokens = _factory.Services.GetRequiredService<ITokenService>();
        var user = await users.GetAsync(userId) ?? await users.CreateAsync(userId: userId, role: "user");
        return (await tokens.IssueAsync(user.UserId, name: "test")).Plaintext;
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, HttpContent? body = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = body;
        return _client.SendAsync(req);
    }
}
