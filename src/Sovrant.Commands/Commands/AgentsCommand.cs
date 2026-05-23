using System.Globalization;
using System.Text;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;

namespace Sovrant.Commands.Commands;

/// <summary>Lists agents, shows details, or runs one via <c>/agent run &lt;name&gt; [prompt]</c>.</summary>
public sealed class AgentsCommand : ISlashCommand
{
    private readonly AgentTemplateRegistry _templates;
    private readonly AdHocAgentRunner? _runner;

    public AgentsCommand(AgentTemplateRegistry templates, AdHocAgentRunner? runner = null)
    {
        _templates = templates;
        _runner = runner;
    }

    public string Name => "agents";
    public IReadOnlyList<string> Aliases => ["agent", "templates"];
    public string Description => "List agents, show details, or run: /agent run <name> [prompt]";
    public string Category => "Advanced";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        args = args.Trim();

        if (args.StartsWith("run ", StringComparison.OrdinalIgnoreCase))
            return await RunAgentAsync(args[4..].Trim(), ct).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(args))
            return ShowTemplate(args);

        return ListTemplates();
    }

    private async Task<SlashCommandResult> RunAgentAsync(string argsAfterRun, CancellationToken ct)
    {
        // Syntax: run <name> [prompt...]
        var parts = argsAfterRun.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return new SlashCommandResult("Usage: /agent run <name> [prompt]");

        var name = parts[0];
        var prompt = parts.Length > 1 ? parts[1].Trim() : null;

        var template = _templates.TryGet(name);
        if (template is null)
            return new SlashCommandResult(
                string.Create(CultureInfo.InvariantCulture, $"Agent '{name}' not found. Use /agents to list all."));

        if (string.IsNullOrWhiteSpace(prompt))
            return new SlashCommandResult(
                string.Create(CultureInfo.InvariantCulture,
                    $"Agent '{name}' found. Provide a prompt to run it: /agent run {name} <your task>"));

        if (_runner is null)
            return new SlashCommandResult("Agent runner not available in this context.");

        var result = await _runner.RunAsync(
            templateName: name,
            prompt: prompt,
            workspaceId: string.Empty,
            projectId: null,
            userId: "cli",
            ct: ct).ConfigureAwait(false);

        if (result.Success)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CultureInfo.InvariantCulture, $"# Agent: {name}");
            sb.AppendLine();
            sb.Append(result.Output);
            return new SlashCommandResult(sb.ToString());
        }

        return new SlashCommandResult(
            string.Create(CultureInfo.InvariantCulture, $"Agent run failed: {result.Error}"));
    }

    private SlashCommandResult ListTemplates()
    {
        var all = _templates.All;
        if (all.Count == 0)
            return new SlashCommandResult("No agents loaded.");

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Agents ({all.Count})");
        sb.AppendLine();
        sb.AppendLine("| Name | Role | Level | Tools |");
        sb.AppendLine("|------|------|-------|-------|");

        foreach (var t in all.OrderBy(t => t.Role.ToString(), StringComparer.OrdinalIgnoreCase)
                             .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var tools = t.AllowedTools.Count == 0 ? "all" : string.Join(", ", t.AllowedTools);
            if (tools.Length > 30)
                tools = string.Concat(tools.AsSpan(0, 27), "...");
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {t.Name} | {t.Role} | {t.RecommendedLevel} | {tools} |");
        }

        sb.AppendLine();
        sb.Append("Use /agent <name> for details · /agent run <name> <prompt> to run one-shot.");

        return new SlashCommandResult(sb.ToString());
    }

    private SlashCommandResult ShowTemplate(string name)
    {
        var t = _templates.TryGet(name);
        if (t is null)
            return new SlashCommandResult(
                string.Create(CultureInfo.InvariantCulture, $"Agent '{name}' not found. Use /agents to list all."));

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {t.Name}");
        if (!string.IsNullOrWhiteSpace(t.Description))
        {
            sb.AppendLine();
            sb.AppendLine(t.Description);
        }
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Role | {t.Role} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Level | {t.RecommendedLevel} |");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"| Tools | {(t.AllowedTools.Count == 0 ? "all (unrestricted)" : string.Join(", ", t.AllowedTools))} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| Source | {(t.IsBuiltIn ? "built-in" : "user-defined")} |");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## System Prompt");
        sb.AppendLine();
        sb.Append(t.SystemPrompt);

        return new SlashCommandResult(sb.ToString());
    }
}
