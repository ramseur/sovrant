using System.Globalization;
using System.Text.Json;
using Sovrant.Agents.Models;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Teams;

/// <summary>
/// Phase 52 — SQLite-backed <see cref="ITeamRegistry"/> that replaces
/// <see cref="InMemoryTeamRegistry"/> as the default DI binding. Teams
/// and members survive restarts and are scoped by workspace/project.
/// </summary>
internal sealed class SqliteTeamRegistry : ITeamRegistry
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public SqliteTeamRegistry(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ── Team-level ──────────────────────────────────────────────────────

    public string CreateTeam(TeamInfo team)
    {
        ArgumentNullException.ThrowIfNull(team);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO teams (team_id, workspace_id, project_id, name, description, origin, created_by, created_at)
            VALUES ($id, $ws, $proj, $name, $desc, $origin, $createdBy, $createdAt)
            """;
        cmd.Parameters.AddWithValue("$id", team.Id);
        cmd.Parameters.AddWithValue("$ws", team.WorkspaceId);
        cmd.Parameters.AddWithValue("$proj", (object?)team.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", team.Name);
        cmd.Parameters.AddWithValue("$desc", (object?)team.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$origin", team.Origin);
        cmd.Parameters.AddWithValue("$createdBy", team.CreatedBy);
        cmd.Parameters.AddWithValue("$createdAt", team.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();

        return team.Id;
    }

    public TeamInfo? GetTeam(string teamId)
    {
        ArgumentNullException.ThrowIfNull(teamId);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT team_id, workspace_id, project_id, name, description, origin, created_by, created_at FROM teams WHERE team_id = $id";
        cmd.Parameters.AddWithValue("$id", teamId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadTeam(r) : null;
    }

    public IReadOnlyList<TeamInfo> ListTeams(string? workspaceId = null)
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();

        if (workspaceId is not null)
        {
            cmd.CommandText = "SELECT team_id, workspace_id, project_id, name, description, origin, created_by, created_at FROM teams WHERE workspace_id = $ws ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("$ws", workspaceId);
        }
        else
        {
            cmd.CommandText = "SELECT team_id, workspace_id, project_id, name, description, origin, created_by, created_at FROM teams ORDER BY created_at DESC";
        }

        using var r = cmd.ExecuteReader();
        var list = new List<TeamInfo>();
        while (r.Read()) list.Add(ReadTeam(r));
        return list;
    }

    public bool RemoveTeam(string teamId)
    {
        ArgumentNullException.ThrowIfNull(teamId);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        // CASCADE on team_members FK handles member cleanup
        cmd.CommandText = "DELETE FROM teams WHERE team_id = $id";
        cmd.Parameters.AddWithValue("$id", teamId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<TeamMemberInfo> GetTeamMembers(string teamId)
    {
        ArgumentNullException.ThrowIfNull(teamId);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT member_id, team_id, workspace_id, project_id, name, role, template,
                   system_prompt, allowed_tools, model_level, created_by, created_at, last_used_at, status
            FROM team_members WHERE team_id = $teamId ORDER BY created_at
            """;
        cmd.Parameters.AddWithValue("$teamId", teamId);
        using var r = cmd.ExecuteReader();
        var list = new List<TeamMemberInfo>();
        while (r.Read()) list.Add(ReadMember(r));
        return list;
    }

    // ── Member-level ────────────────────────────────────────────────────

    public string RegisterMember(TeamMemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO team_members
                (member_id, team_id, workspace_id, project_id, name, role, template,
                 system_prompt, allowed_tools, model_level, created_by, created_at, status)
            VALUES ($id, $teamId, $ws, $proj, $name, $role, $template,
                    $prompt, $tools, $model, $createdBy, $createdAt, $status)
            """;
        cmd.Parameters.AddWithValue("$id", member.Id);
        cmd.Parameters.AddWithValue("$teamId", (object?)member.TeamId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ws", (object?)member.WorkspaceId ?? "");
        cmd.Parameters.AddWithValue("$proj", (object?)member.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$name", member.Name);
        cmd.Parameters.AddWithValue("$role", RoleToString(member.Role));
        cmd.Parameters.AddWithValue("$template", (object?)member.Template ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$prompt", member.SystemPrompt);
        cmd.Parameters.AddWithValue("$tools", member.AllowedTools is { Count: > 0 }
            ? JsonSerializer.Serialize(member.AllowedTools) : DBNull.Value);
        cmd.Parameters.AddWithValue("$model", (object?)member.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdBy", (object?)member.CreatedBy ?? "");
        cmd.Parameters.AddWithValue("$createdAt", member.CreatedAt.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$status", member.Status switch
        {
            TeamMemberStatus.Running => "running",
            TeamMemberStatus.Completed => "completed",
            TeamMemberStatus.Failed => "failed",
            _ => "active",
        });
        cmd.ExecuteNonQuery();

        return member.Id;
    }

    public bool RemoveMember(string memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM team_members WHERE member_id = $id";
        cmd.Parameters.AddWithValue("$id", memberId);
        return cmd.ExecuteNonQuery() > 0;
    }

    public TeamMemberInfo? GetMember(string memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT member_id, team_id, workspace_id, project_id, name, role, template,
                   system_prompt, allowed_tools, model_level, created_by, created_at, last_used_at, status
            FROM team_members WHERE member_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", memberId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadMember(r) : null;
    }

    public IReadOnlyList<TeamMemberInfo> GetAllMembers()
    {
        using var conn = _connectionFactory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT member_id, team_id, workspace_id, project_id, name, role, template,
                   system_prompt, allowed_tools, model_level, created_by, created_at, last_used_at, status
            FROM team_members ORDER BY created_at
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<TeamMemberInfo>();
        while (r.Read()) list.Add(ReadMember(r));
        return list;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static TeamInfo ReadTeam(Microsoft.Data.Sqlite.SqliteDataReader r) => new(
        Id: r.GetString(0),
        WorkspaceId: r.GetString(1),
        ProjectId: r.IsDBNull(2) ? null : r.GetString(2),
        Name: r.GetString(3),
        Description: r.IsDBNull(4) ? null : r.GetString(4),
        Origin: r.GetString(5),
        CreatedBy: r.GetString(6),
        CreatedAt: DateTimeOffset.Parse(r.GetString(7), CultureInfo.InvariantCulture));

    private static TeamMemberInfo ReadMember(Microsoft.Data.Sqlite.SqliteDataReader r)
    {
        var toolsJson = r.IsDBNull(8) ? null : r.GetString(8);
        IReadOnlyList<string>? allowedTools = null;
        if (toolsJson is not null)
        {
            allowedTools = JsonSerializer.Deserialize<List<string>>(toolsJson);
        }

        return new TeamMemberInfo
        {
            Id = r.GetString(0),
            TeamId = r.IsDBNull(1) ? null : r.GetString(1),
            WorkspaceId = r.IsDBNull(2) ? null : r.GetString(2),
            ProjectId = r.IsDBNull(3) ? null : r.GetString(3),
            Name = r.GetString(4),
            Role = ParseRole(r.GetString(5)),
            Template = r.IsDBNull(6) ? null : r.GetString(6),
            SystemPrompt = r.GetString(7),
            AllowedTools = allowedTools,
            Model = r.IsDBNull(9) ? null : r.GetString(9),
            CreatedBy = r.IsDBNull(10) ? null : r.GetString(10),
            CreatedAt = DateTimeOffset.Parse(r.GetString(11), CultureInfo.InvariantCulture),
            Status = ParseStatus(r.IsDBNull(13) ? "active" : r.GetString(13)),
        };
    }

    private static AgentRole ParseRole(string role) => role switch
    {
        "planner" => AgentRole.Planner,
        "coder" => AgentRole.Coder,
        "reviewer" => AgentRole.Reviewer,
        "tester" => AgentRole.Tester,
        "researcher" => AgentRole.Researcher,
        _ => AgentRole.General,
    };

    private static string RoleToString(AgentRole role) => role switch
    {
        AgentRole.Planner => "planner",
        AgentRole.Coder => "coder",
        AgentRole.Reviewer => "reviewer",
        AgentRole.Tester => "tester",
        AgentRole.Researcher => "researcher",
        _ => "general",
    };

    private static TeamMemberStatus ParseStatus(string status) => status switch
    {
        "running" => TeamMemberStatus.Running,
        "completed" => TeamMemberStatus.Completed,
        "failed" => TeamMemberStatus.Failed,
        _ => TeamMemberStatus.Idle,
    };
}
