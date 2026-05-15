using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Sovrant.Commands.Commands;

/// <summary>Lists all available slash commands grouped by category.</summary>
public sealed class HelpCommand : ISlashCommand
{
    private readonly IServiceProvider _serviceProvider;

    public HelpCommand(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public string Name => "help";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show this help message.";

    // Category display order.
    private static readonly string[] s_categoryOrder =
        ["Session", "Memory", "Config", "Tools", "Advanced", "General"];

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var commands = _serviceProvider.GetServices<ISlashCommand>()
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grouped = commands
            .GroupBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var spectreBuilder = new StringBuilder();
        var mdBuilder = new StringBuilder();
        mdBuilder.AppendLine("# Available Commands");
        mdBuilder.AppendLine();
        var first = true;

        foreach (var category in s_categoryOrder)
        {
            if (!grouped.TryGetValue(category, out var cmds))
                continue;

            if (!first) spectreBuilder.AppendLine();
            first = false;

            spectreBuilder.AppendLine(CultureInfo.InvariantCulture, $"[bold]{category}[/]");

            mdBuilder.AppendLine(CultureInfo.InvariantCulture, $"## {category}");
            mdBuilder.AppendLine();
            mdBuilder.AppendLine("| Command | Description |");
            mdBuilder.AppendLine("|---------|-------------|");

            foreach (var cmd in cmds)
            {
                var aliases = cmd.Aliases.Count > 0
                    ? $" ({string.Join(", ", cmd.Aliases.Select(a => "/" + a))})"
                    : string.Empty;
                spectreBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"  [teal]/{cmd.Name,-14}[/] {Markup.Escape(cmd.Description)}{Markup.Escape(aliases)}");
                mdBuilder.AppendLine(CultureInfo.InvariantCulture,
                    $"| /{cmd.Name} | {cmd.Description}{aliases} |");
            }

            mdBuilder.AppendLine();
        }

        // Render via Spectre markup to console for CLI.
        AnsiConsole.Markup(spectreBuilder.ToString());

        // Return markdown output for non-console consumers (desktop, web).
        return Task.FromResult(new SlashCommandResult(Output: mdBuilder.ToString()));
    }
}
