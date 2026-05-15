using System.Globalization;
using System.Text;
using Sovrant.Runtime.Tools;

namespace Sovrant.Commands.Commands;

/// <summary>Lists all registered tools or shows details of a specific tool.</summary>
public sealed class ToolsCommand : ISlashCommand
{
    private readonly IToolRegistry _registry;

    public ToolsCommand(IToolRegistry registry) => _registry = registry;

    public string Name => "tools";
    public IReadOnlyList<string> Aliases => ["tool"];
    public string Description => "List all registered tools.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var definitions = _registry.GetDefinitions();
        if (definitions.Count == 0)
            return Task.FromResult(new SlashCommandResult("No tools registered."));

        if (!string.IsNullOrWhiteSpace(args))
        {
            var name = args.Trim();
            var match = definitions.FirstOrDefault(
                d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return Task.FromResult(
                    new SlashCommandResult($"Tool '{name}' not found. Use /tools to list all."));

            var detail = new StringBuilder();
            detail.AppendLine(CultureInfo.InvariantCulture, $"# {match.Name}");
            detail.AppendLine();
            detail.AppendLine(CultureInfo.InvariantCulture,
                $"{match.Description ?? "(no description)"}");
            detail.AppendLine();
            detail.AppendLine("**Input Schema:**");
            detail.AppendLine();
            detail.Append(match.InputSchema.ToString());
            return Task.FromResult(new SlashCommandResult(detail.ToString()));
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Tools ({definitions.Count})");
        sb.AppendLine();
        sb.AppendLine("| Name | Description |");
        sb.AppendLine("|------|-------------|");

        foreach (var d in definitions.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var desc = d.Description ?? "";
            if (desc.Length > 60)
                desc = string.Concat(desc.AsSpan(0, 57), "...");
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {d.Name} | {desc} |");
        }

        sb.AppendLine();
        sb.Append("Use /tools (name) for details.");

        return Task.FromResult(new SlashCommandResult(sb.ToString()));
    }
}
