using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Agents.Models;
using Sovrant.Agents.Orchestration;
using Sovrant.Agents.Teams;
using Sovrant.Runtime.Storage;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 52 — team CRUD and team run endpoints.
/// <list type="bullet">
///   <item><c>POST /v1/teams</c> — create a team</item>
///   <item><c>GET /v1/teams</c> — list teams</item>
///   <item><c>GET /v1/teams/{id}</c> — get team + members</item>
///   <item><c>DELETE /v1/teams/{id}</c> — delete a team</item>
///   <item><c>POST /v1/teams/{id}/members</c> — add a member</item>
///   <item><c>GET /v1/teams/{id}/members</c> — list members</item>
///   <item><c>POST /v1/teams/{id}/runs</c> — start a run against a team</item>
///   <item><c>GET /v1/runs/{id}</c> — get any run by ID</item>
///   <item><c>GET /v1/runs</c> — list runs with filters</item>
/// </list>
/// </summary>
internal static class TeamRoutes
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void Map(WebApplication app)
    {
        // ── Team CRUD ───────────────────────────────────────────────────

        app.MapPost("/v1/teams", (CreateTeamRequest req, ITeamRegistry registry) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required." });

            var team = new TeamInfo(
                Id: $"team-{Guid.NewGuid():N}",
                WorkspaceId: req.WorkspaceId ?? "",
                ProjectId: req.ProjectId,
                Name: req.Name,
                Description: req.Description,
                Origin: req.Origin ?? "user",
                CreatedBy: req.CreatedBy ?? "system",
                CreatedAt: DateTimeOffset.UtcNow);

            registry.CreateTeam(team);
            return Results.Json(team, s_jsonOptions, statusCode: 201);
        });

        app.MapGet("/v1/teams", (string? workspaceId, ITeamRegistry registry) =>
        {
            var teams = registry.ListTeams(workspaceId);
            return Results.Json(new { teams }, s_jsonOptions);
        });

        app.MapGet("/v1/teams/{id}", (string id, ITeamRegistry registry) =>
        {
            var team = registry.GetTeam(id);
            if (team is null) return Results.NotFound(new { error = $"team '{id}' not found" });
            var members = registry.GetTeamMembers(id);
            return Results.Json(new { team, members }, s_jsonOptions);
        });

        app.MapDelete("/v1/teams/{id}", (string id, ITeamRegistry registry) =>
        {
            return registry.RemoveTeam(id)
                ? Results.Ok(new { deleted = true })
                : Results.NotFound(new { error = $"team '{id}' not found" });
        });

        // ── Team members ────────────────────────────────────────────────

        app.MapPost("/v1/teams/{id}/members", (string id, AddMemberRequest req, ITeamRegistry registry) =>
        {
            var team = registry.GetTeam(id);
            if (team is null) return Results.NotFound(new { error = $"team '{id}' not found" });

            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name is required." });

            var member = new TeamMemberInfo
            {
                Id = $"member-{Guid.NewGuid():N}",
                TeamId = id,
                WorkspaceId = team.WorkspaceId,
                ProjectId = team.ProjectId,
                Name = req.Name,
                Role = Enum.TryParse<AgentRole>(req.Role, ignoreCase: true, out var role) ? role : AgentRole.General,
                Template = req.Template,
                SystemPrompt = req.SystemPrompt ?? "You are a helpful agent.",
                CreatedBy = req.CreatedBy ?? "system",
            };

            registry.RegisterMember(member);
            return Results.Json(new { member_id = member.Id, name = member.Name }, s_jsonOptions, statusCode: 201);
        });

        app.MapGet("/v1/teams/{id}/members", (string id, ITeamRegistry registry) =>
        {
            var team = registry.GetTeam(id);
            if (team is null) return Results.NotFound(new { error = $"team '{id}' not found" });
            var members = registry.GetTeamMembers(id);
            return Results.Json(new { members }, s_jsonOptions);
        });

        // ── Team runs ───────────────────────────────────────────────────

        app.MapPost("/v1/teams/{id}/runs", async (
            string id,
            TeamRunRequest req,
            ITeamRegistry registry,
            IAgentOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var team = registry.GetTeam(id);
            if (team is null) return Results.NotFound(new { error = $"team '{id}' not found" });

            if (string.IsNullOrWhiteSpace(req.Goal))
                return Results.BadRequest(new { error = "goal is required." });

            var request = new EnsembleRunRequest
            {
                Goal = req.Goal,
                TeamId = id,
                WorkspaceId = team.WorkspaceId,
                ProjectId = team.ProjectId,
                UserId = req.UserId ?? team.CreatedBy,
                Decompose = req.Decompose,
                LockFiles = req.LockFiles,
                QualityGate = req.QualityGate,
                MaxParallel = req.MaxParallel,
            };

            var result = await orchestrator.RunAsync(request, ct);
            return Results.Json(new
            {
                run_id = result.RunId,
                status = result.SwarmResult.Status.ToString(),
                output = result.SwarmResult.CombinedOutput,
                tokens_used = result.SwarmResult.TotalTokensUsed,
            }, s_jsonOptions);
        });

        // ── Run queries (any kind) ──────────────────────────────────────

        app.MapGet("/v1/runs/{id}", async (string id, IAgentRunStore store, CancellationToken ct) =>
        {
            var run = await store.GetAsync(id, ct);
            return run is null
                ? Results.NotFound(new { error = $"run '{id}' not found" })
                : Results.Json(run, s_jsonOptions);
        });

        app.MapGet("/v1/runs", async (
            string? workspaceId,
            string? userId,
            string? teamId,
            string? kind,
            string? status,
            int? limit,
            IAgentRunStore store,
            CancellationToken ct) =>
        {
            var filter = new AgentRunFilter(
                WorkspaceId: workspaceId,
                UserId: userId,
                TeamId: teamId,
                Kind: kind,
                Status: status);
            var runs = await store.ListAsync(filter, limit ?? 50, ct);
            return Results.Json(new { runs }, s_jsonOptions);
        });
    }

    // ── Request DTOs ────────────────────────────────────────────────────

    public sealed record CreateTeamRequest(
        string Name,
        string? Description = null,
        string? WorkspaceId = null,
        string? ProjectId = null,
        string? Origin = null,
        string? CreatedBy = null);

    public sealed record AddMemberRequest(
        string Name,
        string? Role = null,
        string? Template = null,
        string? SystemPrompt = null,
        string? CreatedBy = null);

    public sealed record TeamRunRequest(
        string Goal,
        string? UserId = null,
        bool? Decompose = null,
        bool? LockFiles = null,
        bool? QualityGate = null,
        int? MaxParallel = null);
}
