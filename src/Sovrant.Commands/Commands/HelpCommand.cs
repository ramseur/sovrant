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

        var sb = new StringBuilder();
        var first = true;

        foreach (var category in s_categoryOrder)
        {
            if (!grouped.TryGetValue(category, out var cmds))
                continue;

            if (!first) sb.AppendLine();
            first = false;

            sb.AppendLine(CultureInfo.InvariantCulture, $"[bold]{category}[/]");

            foreach (var cmd in cmds)
            {
                var aliases = cmd.Aliases.Count > 0
                    ? $" [grey]({string.Join(", ", cmd.Aliases.Select(a => "/" + a))})[/]"
                    : string.Empty;
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"  [teal]/{cmd.Name,-14}[/] {Markup.Escape(cmd.Description)}{aliases}");
            }
        }

        // Render via Spectre markup so colors work.
        AnsiConsole.Markup(sb.ToString());
        return Task.FromResult(new SlashCommandResult());
    }
}
