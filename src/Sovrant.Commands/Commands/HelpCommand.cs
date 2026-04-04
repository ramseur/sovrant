using System.Globalization;
using System.Text;

namespace Sovrant.Commands.Commands;

/// <summary>Lists all available slash commands.</summary>
public sealed class HelpCommand : ISlashCommand
{
    private readonly SlashCommandDispatcher _dispatcher;

    public HelpCommand(SlashCommandDispatcher dispatcher) => _dispatcher = dispatcher;

    public string Name => "help";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show this help message.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        sb.AppendLine();

        foreach (var cmd in _dispatcher.Commands)
        {
            var aliases = cmd.Aliases.Count > 0
                ? $" (/{string.Join(", /", cmd.Aliases)})"
                : string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  /{cmd.Name,-14} {cmd.Description}{aliases}");
        }

        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }
}
