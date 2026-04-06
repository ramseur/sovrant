using System.Globalization;
using System.Text;
using Sovrant.Agents.Templates;

namespace Sovrant.Commands.Commands;

/// <summary>Lists agent templates or shows details of a specific template.</summary>
public sealed class AgentsCommand : ISlashCommand
{
    private readonly AgentTemplateRegistry _templates;

    public AgentsCommand(AgentTemplateRegistry templates) => _templates = templates;

    public string Name => "agents";
    public IReadOnlyList<string> Aliases => ["agent", "templates"];
    public string Description => "List agent templates or show details of one.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(args))
            return Task.FromResult(ShowTemplate(args.Trim()));

        return Task.FromResult(ListTemplates());
    }

    private SlashCommandResult ListTemplates()
    {
        var all = _templates.All;
        if (all.Count == 0)
            return new SlashCommandResult("No agent templates loaded.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"{"Name",-25} {"Role",-12} {"Level",-10} {"Tools"}");
        sb.AppendLine(new string('-', 65));

        foreach (var t in all.OrderBy(t => t.Role.ToString(), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var tools = t.AllowedTools.Count == 0 ? "all" : string.Join(", ", t.AllowedTools);
            if (tools.Length > 30)
                tools = string.Concat(tools.AsSpan(0, 27), "...");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{t.Name,-25} {t.Role,-12} {t.RecommendedLevel,-10} {tools}");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"{all.Count} templates. Use /agents <name> for details.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTemplate(string name)
    {
        var t = _templates.TryGet(name);
        if (t is null)
            return new SlashCommandResult($"Agent template '{name}' not found. Use /agents to list all.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Name:   {t.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Role:   {t.Role}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Level:  {t.RecommendedLevel}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Tools:  {(t.AllowedTools.Count == 0 ? "all (unrestricted)" : string.Join(", ", t.AllowedTools))}");
        sb.AppendLine();
        sb.AppendLine("System Prompt:");

        var prompt = t.SystemPrompt;
        if (prompt.Length > 500)
            prompt = string.Concat(prompt.AsSpan(0, 497), "...");
        sb.Append(prompt);

        return new SlashCommandResult(sb.ToString());
    }
}
