using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Runtime.Storage;

namespace Sovrant.Server.Tests;

/// <summary>
/// Tests for the Phase 51 engine introspection endpoints. These talk to
/// the real SqliteRuntimeTraceStore wired up inside the WebApplicationFactory,
/// which is backed by the per-test in-memory SQLite DB.
/// </summary>
public sealed class EngineRoutesTests : IClassFixture<SovrantWebAppFactory>
{
    private readonly HttpClient _client;
    private readonly SovrantWebAppFactory _factory;

    public EngineRoutesTests(SovrantWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpRequestMessage Auth(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-token-123");
        return req;
    }

    private async Task SeedAsync(string runId, string entryType, int? stepIndex,
        string planId = "plan-1", int planVersion = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IRuntimeTraceStore>();
        await store.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: runId,
            PlanId: planId,
            PlanVersion: planVersion,
            StepIndex: stepIndex,
            EntryType: entryType,
            Payload: "{}",
            Timestamp: DateTimeOffset.UtcNow,
            SessionId: "sess-1",
            WorkspaceId: "ws-1",
            ProjectId: "proj-1"));
    }

    [Fact]
    public async Task GetTrace_MissingRun_Returns404()
    {
        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/engine/runs/nope/trace"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetTrace_ReturnsEntriesInOrder()
    {
        var runId = $"run-trace-{Guid.NewGuid():N}";
        await SeedAsync(runId, "plan_created", null);
        await SeedAsync(runId, "step_started", 0);
        await SeedAsync(runId, "step_completed", 0);
        await SeedAsync(runId, "run_completed", null);

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, $"/v1/engine/runs/{runId}/trace"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal(4, entries.GetArrayLength());
        Assert.Equal("plan_created", entries[0].GetProperty("entry_type").GetString());
        Assert.Equal("run_completed", entries[3].GetProperty("entry_type").GetString());
    }

    [Fact]
    public async Task ListInFlight_ReturnsOrphanedRuns()
    {
        var runId = $"run-inflight-{Guid.NewGuid():N}";
        await SeedAsync(runId, "step_started", 0);

        var resp = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/engine/runs/in-flight"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains(runId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecoverRuns_ClosesInFlightRuns()
    {
        var runId = $"run-recover-{Guid.NewGuid():N}";
        await SeedAsync(runId, "step_started", 0);

        var resp = await _client.SendAsync(Auth(HttpMethod.Post, "/v1/engine/runs/recover"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains(runId, body, StringComparison.Ordinal);

        // Verify the run is no longer in-flight after the recovery call.
        var followup = await _client.SendAsync(Auth(HttpMethod.Get, "/v1/engine/runs/in-flight"));
        var followupBody = await followup.Content.ReadAsStringAsync();
        Assert.DoesNotContain(runId, followupBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteRun_RemovesTraceRows()
    {
        var runId = $"run-delete-{Guid.NewGuid():N}";
        await SeedAsync(runId, "plan_created", null);
        await SeedAsync(runId, "step_started", 0);
        await SeedAsync(runId, "step_completed", 0);

        var resp = await _client.SendAsync(Auth(HttpMethod.Delete, $"/v1/engine/runs/{runId}"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("deleted_rows").GetInt32() >= 3);

        // Follow-up GET should now 404.
        var getResp = await _client.SendAsync(Auth(HttpMethod.Get, $"/v1/engine/runs/{runId}/trace"));
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }
}
