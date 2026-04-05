using System.Globalization;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Runs the swarm orchestrator from the REPL by injecting a user message
/// that triggers the Swarm tool.
/// Usage: <c>/swarm &lt;prompt&gt; [--dry-run]</c>
/// </summary>
public sealed class SwarmCommand : ISlashCommand
{
    public string Name => "swarm";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Decompose and execute a task via parallel agent swarm.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Task.FromResult(new SlashCommandResult(
                "Usage: /swarm <prompt> [--dry-run]\n" +
                "  --dry-run  Show decomposed plan without executing"));
        }

        var dryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

        // Strip flags from the prompt
        var prompt = string.Join(' ', parts.Where(p =>
            !p.StartsWith("--", StringComparison.Ordinal)));

        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult(new SlashCommandResult("Error: no prompt provided."));

        // Inject the prompt as a user message directing use of the Swarm tool
        var dryRunSuffix = dryRun ? " Use dry_run mode to show the plan only." : string.Empty;
        return Task.FromResult(new SlashCommandResult(
            InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                $"Use the Swarm tool to execute: {prompt}{dryRunSuffix}")));
    }
}
