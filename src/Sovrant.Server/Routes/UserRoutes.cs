using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Users;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server.Routes;

/// <summary>
/// Phase 37 — user management endpoints.
///
/// <para><b>Auth model:</b> Every endpoint here is gated by the existing
/// <c>SOVRANT_TOKEN</c> bearer middleware. There is no per-user authentication
/// yet — any holder of the shared token can manage any user. Per-user bearer
/// tokens, role enforcement, and admin-only routes are deferred to Phase 38.</para>
///
/// <para><b>Soft-delete only:</b> <c>DELETE</c> flips <c>status='inactive'</c>;
/// it never removes the row. This preserves all FK references and audit history.</para>
///
/// <para><b>Mass-assignment protection:</b> request bodies use POCO DTOs that only
/// expose client-writable fields. <c>user_id</c>, <c>created_at</c>, and
/// <c>updated_at</c> are server-controlled and cannot be set via JSON.</para>
/// </summary>
internal static class UserRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/v1/users", CreateUser);
        app.MapGet("/v1/users", ListUsers);
        app.MapGet("/v1/users/{id}", GetUser);
        app.MapPut("/v1/users/{id}", UpdateUser);
        app.MapDelete("/v1/users/{id}", DeactivateUser);
        app.MapPost("/v1/users/{id}/reactivate", ReactivateUser);

        app.MapGet("/v1/users/{id}/sessions", ListUserSessions);
        app.MapGet("/v1/users/{id}/usage", GetUserUsage);
        app.MapGet("/v1/users/{id}/audit", ListUserAudit);
    }

    // ── CRUD ───────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateUser(
        CreateUserRequest req,
        IUserService users,
        IWorkspaceService workspaces,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (string.IsNullOrWhiteSpace(req.Username))
            return Results.BadRequest(new { error = "username is required." });

        try
        {
            var user = await users.CreateAsync(
                username: req.Username,
                email: req.Email,
                role: req.Role ?? "user",
                team: req.Team,
                ct: ct).ConfigureAwait(false);

            // Auto-create personal workspace for parity with the seeded default user.
            // Without this, workspace-scoped middleware would 404 on this user's first request.
            try
            {
                await workspaces.CreatePersonalWorkspaceAsync(user.UserId, ct).ConfigureAwait(false);
            }
            catch (SqliteException ex)
            {
                // The personal workspace insert is idempotent, but log unexpected failures.
                var log = loggerFactory.CreateLogger("Sovrant.Server.UserRoutes");
                log.LogWarning(ex, "Failed to auto-create personal workspace for {UserId}", user.UserId);
            }

            return Results.Created($"/v1/users/{user.UserId}", user);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 19 = SQLITE_CONSTRAINT — username or email uniqueness collision.
            return Results.Conflict(new { error = "A user with that username or email already exists." });
        }
    }

    private static async Task<IResult> ListUsers(
        string? status,
        string? role,
        string? team,
        int? limit,
        int? offset,
        IUserService users,
        CancellationToken ct)
    {
        var filter = new UserListFilter
        {
            Status = status,
            Role = role,
            Team = team,
            Limit = limit ?? 100,
            Offset = offset ?? 0,
        };

        try
        {
            var list = await users.ListAsync(filter, ct).ConfigureAwait(false);
            var total = await users.CountAsync(filter, ct).ConfigureAwait(false);
            return Results.Ok(new { users = list, count = list.Count, total });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetUser(
        string id, IUserService users, CancellationToken ct)
    {
        var profile = await users.GetProfileAsync(id, ct).ConfigureAwait(false);
        return profile is null
            ? Results.NotFound(new { error = "User not found." })
            : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateUser(
        string id, UpdateUserRequest req, IUserService users, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        try
        {
            var updated = await users.UpdateAsync(
                userId: id,
                username: req.Username,
                email: req.Email,
                role: req.Role,
                team: req.Team,
                status: req.Status,
                ct: ct).ConfigureAwait(false);

            return updated is null
                ? Results.NotFound(new { error = "User not found." })
                : Results.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return Results.Conflict(new { error = "A user with that username or email already exists." });
        }
    }

    private static async Task<IResult> DeactivateUser(
        string id, IUserService users, CancellationToken ct)
    {
        // Self-protection: refuse to deactivate the boot identity, otherwise
        // the server will be unable to seed sessions on its next restart.
        var bootIdentity = Environment.GetEnvironmentVariable("SOVRANT_USER_ID")
            ?? Environment.UserName;
        if (string.Equals(id, bootIdentity, StringComparison.Ordinal))
        {
            return Results.Conflict(new
            {
                error = "Cannot deactivate the server boot identity (the SOVRANT_USER_ID / OS-username row).",
            });
        }

        var deactivated = await users.DeactivateAsync(id, ct).ConfigureAwait(false);
        return deactivated
            ? Results.Ok(new { deactivated = id })
            : Results.NotFound(new { error = "User not found or already inactive." });
    }

    private static async Task<IResult> ReactivateUser(
        string id, IUserService users, CancellationToken ct)
    {
        var reactivated = await users.ReactivateAsync(id, ct).ConfigureAwait(false);
        return reactivated
            ? Results.Ok(new { reactivated = id })
            : Results.NotFound(new { error = "User not found or not inactive." });
    }

    // ── Per-user data views ────────────────────────────────────────────────

    private static async Task<IResult> ListUserSessions(
        string id, int? limit, int? offset, IUserService users, CancellationToken ct)
    {
        var sessions = await users.ListSessionsAsync(id, limit ?? 100, offset ?? 0, ct).ConfigureAwait(false);
        return Results.Ok(new { sessions, count = sessions.Count });
    }

    private static async Task<IResult> GetUserUsage(
        string id,
        string? model,
        DateTimeOffset? from,
        DateTimeOffset? to,
        IUserService users,
        CancellationToken ct)
    {
        var usage = await users.GetUsageAsync(id, model, from, to, ct).ConfigureAwait(false);
        return Results.Ok(usage);
    }

    private static async Task<IResult> ListUserAudit(
        string id, int? limit, IUserService users, CancellationToken ct)
    {
        var events = await users.ListAuditEventsAsync(id, limit ?? 100, ct).ConfigureAwait(false);
        return Results.Ok(new { events, count = events.Count });
    }
}

// ── Request DTOs ───────────────────────────────────────────────────────────
//
// These DTOs only expose client-writable fields. user_id, created_at, and
// updated_at are server-controlled and cannot be set through the API.

internal sealed class CreateUserRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }
}

internal sealed class UpdateUserRequest
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("team")]
    public string? Team { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
