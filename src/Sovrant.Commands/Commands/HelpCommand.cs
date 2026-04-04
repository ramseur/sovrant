using System.Globalization;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Sovrant.Commands.Commands;

/// <summary>Lists all available slash commands.</summary>
public sealed class HelpCommand : ISlashCommand
{
    private readonly IServiceProvider _serviceProvider;

    public HelpCommand(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public string Name => "help";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Show this help message.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        // Resolve lazily to avoid the circular dependency that would occur at construction time
        // (HelpCommand is itself an ISlashCommand).
        var commands = _serviceProvider.GetServices<ISlashCommand>()
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Name);

        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        sb.AppendLine();

        foreach (var cmd in commands)
        {
            var aliases = cmd.Aliases.Count > 0
                ? $" (/{string.Join(", /", cmd.Aliases)})"
                : string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  /{cmd.Name,-14} {cmd.Description}{aliases}");
        }

        return Task.FromResult(new SlashCommandResult(sb.ToString().TrimEnd()));
    }
}
