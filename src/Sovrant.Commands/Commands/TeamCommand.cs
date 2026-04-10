using System.Globalization;
using System.Text;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Phase 52 — enhanced team slash command with full CRUD:
/// <c>/team list</c>, <c>/team show &lt;id&gt;</c>, <c>/team create &lt;name&gt;</c>,
/// <c>/team delete &lt;id&gt;</c>, <c>/team members &lt;id&gt;</c>.
/// Backward-compatible: bare <c>/team</c> still lists members, <c>/team &lt;id&gt;</c> shows a member.
/// </summary>
public sealed class TeamCommand : ISlashCommand
{
    private readonly ITeamRegistry _registry;

    public TeamCommand(ITeamRegistry registry) => _registry = registry;

    public string Name => "team";
    public IReadOnlyList<string> Aliases => ["teams"];
    public string Description => "Manage teams and team members.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Task.FromResult(ListTeams());

        return parts[0] switch
        {
            "list" => Task.FromResult(ListTeams()),
            "show" => Task.FromResult(parts.Length > 1 ? ShowTeam(parts[1]) : Err("usage: /team show <team_id>")),
            "create" => Task.FromResult(parts.Length > 1 ? CreateTeam(parts[1..]) : Err("usage: /team create <name> [workspace_id]")),
            "delete" => Task.FromResult(parts.Length > 1 ? DeleteTeam(parts[1]) : Err("usage: /team delete <team_id>")),
            "members" => Task.FromResult(parts.Length > 1 ? ListTeamMembers(parts[1]) : ListAllMembers()),
            _ => Task.FromResult(ShowTeamOrMember(parts[0])),
        };
    }

    private SlashCommandResult ListTeams()
    {
        var teams = _registry.ListTeams();
        if (teams.Count == 0)
        {
            // Fall back to listing members (backward compat)
            return ListAllMembers();
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"ID",-38} {"Name",-20} {"Origin",-16} {"Members"}");
        sb.AppendLine(new string('-', 80));

        foreach (var t in teams)
        {
            var memberCount = _registry.GetTeamMembers(t.Id).Count;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{t.Id,-38} {t.Name,-20} {t.Origin,-16} {memberCount}");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"{teams.Count} team(s). Use /team show <id> for details.");
        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTeam(string id)
    {
        var team = _registry.GetTeam(id);
        if (team is null)
            return ShowMember(id);

        var members = _registry.GetTeamMembers(id);
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Team:        {team.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"ID:          {team.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Origin:      {team.Origin}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Workspace:   {team.WorkspaceId}");
        if (team.ProjectId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Project:     {team.ProjectId}");
        if (team.Description is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Description: {team.Description}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Created:     {team.CreatedAt:u}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Created by:  {team.CreatedBy}");
        sb.AppendLine();

        if (members.Count > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{"Member ID",-38} {"Name",-20} {"Role",-10} {"Template"}");
            sb.AppendLine(new string('-', 80));
            foreach (var m in members)
            {
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"{m.Id,-38} {m.Name,-20} {m.Role,-10} {m.Template ?? "-"}");
            }
        }
        else
        {
            sb.AppendLine("No members.");
        }

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult CreateTeam(string[] rest)
    {
        var name = rest[0];
        var workspaceId = rest.Length > 1 ? rest[1] : "";

        var team = new TeamInfo(
            Id: $"team-{Guid.NewGuid():N}",
            WorkspaceId: workspaceId,
            ProjectId: null,
            Name: name,
            Description: null,
            Origin: "user",
            CreatedBy: "cli",
            CreatedAt: DateTimeOffset.UtcNow);

        _registry.CreateTeam(team);
        return new SlashCommandResult($"Created team '{name}' (ID: {team.Id})");
    }

    private SlashCommandResult DeleteTeam(string id)
    {
        return _registry.RemoveTeam(id)
            ? new SlashCommandResult($"Deleted team '{id}'.")
            : Err($"Team '{id}' not found.");
    }

    private SlashCommandResult ListTeamMembers(string teamId)
    {
        var members = _registry.GetTeamMembers(teamId);
        if (members.Count == 0)
            return new SlashCommandResult($"No members in team '{teamId}'.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"ID",-38} {"Name",-20} {"Role",-10} {"Status"}");
        sb.AppendLine(new string('-', 75));
        foreach (var m in members)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{m.Id,-38} {m.Name,-20} {m.Role,-10} {m.Status}");
        }
        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ListAllMembers()
    {
        var members = _registry.GetAllMembers();
        if (members.Count == 0)
            return new SlashCommandResult("No team members active. Use /team create <name> to create a team.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"ID",-10} {"Name",-20} {"Role",-12} {"Status",-10} {"Model"}");
        sb.AppendLine(new string('-', 65));

        foreach (var m in members)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{m.Id,-10} {m.Name,-20} {m.Role,-12} {m.Status,-10} {m.Model ?? "default"}");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"{members.Count} members.");
        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTeamOrMember(string id)
    {
        var team = _registry.GetTeam(id);
        if (team is not null) return ShowTeam(id);
        return ShowMember(id);
    }

    private SlashCommandResult ShowMember(string id)
    {
        var m = _registry.GetMember(id);
        if (m is null)
            return Err($"'{id}' is not a team or member ID. Use /team list.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"ID:      {m.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Name:    {m.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Role:    {m.Role}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Status:  {m.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Model:   {m.Model ?? "default"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Created: {m.CreatedAt:u}");
        if (m.TeamId is not null)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Team:    {m.TeamId}");

        if (m.AllowedTools is { Count: > 0 })
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"Tools:   {string.Join(", ", m.AllowedTools)}");

        if (m.LastOutput is not null)
        {
            sb.AppendLine();
            sb.AppendLine("Last Output:");
            var output = m.LastOutput;
            if (output.Length > 300)
                output = string.Concat(output.AsSpan(0, 297), "...");
            sb.Append(output);
        }

        if (m.LastError is not null)
        {
            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"Last Error: {m.LastError}");
        }

        return new SlashCommandResult(sb.ToString());
    }

    private static SlashCommandResult Err(string msg) => new(msg);
}
