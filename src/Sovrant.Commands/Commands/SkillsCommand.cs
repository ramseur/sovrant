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
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Skills ({all.Count})");
        sb.AppendLine();
        sb.AppendLine("| Name | Trigger | Description |");
        sb.AppendLine("|------|---------|-------------|");

        foreach (var s in all.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            var trigger = string.IsNullOrEmpty(s.Trigger) ? "--" : s.Trigger;
            var desc = s.Description.Length > 45
                ? string.Concat(s.Description.AsSpan(0, 42), "...")
                : s.Description;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {s.Name} | {trigger} | {desc} |");
        }

        sb.AppendLine();
        sb.Append("Use /skills (name) for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowSkill(string name)
    {
        var s = _skills.TryGetByName(name);
        if (s is null)
            return new SlashCommandResult($"Skill '{name}' not found. Use /skills to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {s.Name}");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"*{s.Description}*");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Trigger | {(string.IsNullOrEmpty(s.Trigger) ? "(none)" : s.Trigger)} |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Agents | {(s.Agents.Count == 0 ? "(none)" : string.Join(", ", s.Agents))} |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Tools | {(s.Tools.Count == 0 ? "all (unrestricted)" : string.Join(", ", s.Tools))} |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Workflow");
        sb.AppendLine();
        sb.Append(s.Body);

        return new SlashCommandResult(sb.ToString());
    }
}
