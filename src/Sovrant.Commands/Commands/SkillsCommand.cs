using System.Globalization;
using System.Text;
using Sovrant.Tools.Skills;

namespace Sovrant.Commands.Commands;

/// <summary>Lists skills or shows details of a specific skill.</summary>
public sealed class SkillsCommand : ISlashCommand
{
    private readonly SkillRegistry _skills;

    public SkillsCommand(SkillRegistry skills) => _skills = skills;

    public string Name => "skills";
    public IReadOnlyList<string> Aliases => ["skill"];
    public string Description => "List skills or show details of one.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(args))
            return Task.FromResult(ShowSkill(args.Trim()));

        return Task.FromResult(ListSkills());
    }

    private SlashCommandResult ListSkills()
    {
        var all = _skills.All;
        if (all.Count == 0)
            return new SlashCommandResult("No skills loaded.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Name",-25} {"Trigger",-12} {"Description"}");
        sb.AppendLine(new string('-', 70));

        foreach (var s in all.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var trigger = string.IsNullOrEmpty(s.Trigger) ? "—" : s.Trigger;
            var desc = s.Description.Length > 35
                ? string.Concat(s.Description.AsSpan(0, 32), "...")
                : s.Description;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{s.Name,-25} {trigger,-12} {desc}");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"{all.Count} skills. Use /skills <name> for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowSkill(string name)
    {
        var s = _skills.TryGetByName(name);
        if (s is null)
            return new SlashCommandResult($"Skill '{name}' not found. Use /skills to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Name:        {s.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Description: {s.Description}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Trigger:     {(string.IsNullOrEmpty(s.Trigger) ? "(none)" : s.Trigger)}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Agents:      {(s.Agents.Count == 0 ? "(none)" : string.Join(", ", s.Agents))}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Tools:       {(s.Tools.Count == 0 ? "all (unrestricted)" : string.Join(", ", s.Tools))}");
        sb.AppendLine();
        sb.AppendLine("Workflow:");

        var body = s.Body;
        if (body.Length > 500)
            body = string.Concat(body.AsSpan(0, 497), "...");
        sb.Append(body);

        return new SlashCommandResult(sb.ToString());
    }
}
