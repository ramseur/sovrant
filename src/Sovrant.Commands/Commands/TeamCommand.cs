using System.Globalization;
using System.Text;
using Sovrant.Agents.Teams;

namespace Sovrant.Commands.Commands;

/// <summary>Lists active team members and their status.</summary>
public sealed class TeamCommand : ISlashCommand
{
    private readonly ITeamRegistry _registry;

    public TeamCommand(ITeamRegistry registry) => _registry = registry;

    public string Name => "team";
    public IReadOnlyList<string> Aliases => ["teams"];
    public string Description => "List active team members and their status.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(args))
            return Task.FromResult(ShowMember(args.Trim()));

        return Task.FromResult(ListMembers());
    }

    private SlashCommandResult ListMembers()
    {
        var members = _registry.GetAllMembers();
        if (members.Count == 0)
            return new SlashCommandResult("No team members active. Use the TeamCreate tool to add agents.");

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
        sb.Append(CultureInfo.InvariantCulture, $"{members.Count} members. Use /team <id> for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowMember(string id)
    {
        var m = _registry.GetMember(id);
        if (m is null)
            return new SlashCommandResult($"Team member '{id}' not found. Use /team to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"ID:      {m.Id}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Name:    {m.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Role:    {m.Role}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Status:  {m.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Model:   {m.Model ?? "default"}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Created: {m.CreatedAt:u}");

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
}
