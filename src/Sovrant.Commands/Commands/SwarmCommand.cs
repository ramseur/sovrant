using System.Globalization;
using Sovrant.Agents.Swarm;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Runs the swarm orchestrator from the REPL by injecting a user message
/// that triggers the Swarm tool. Also supports enable/disable subcommands.
/// Usage: <c>/swarm enable</c>, <c>/swarm disable</c>, <c>/swarm &lt;prompt&gt; [--dry-run]</c>
/// </summary>
public sealed class SwarmCommand : ISlashCommand
{
    private readonly SwarmConfig _config;

    public SwarmCommand(SwarmConfig config) => _config = config;

    public string Name => "swarm";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Enable/disable swarm or run a swarm task.";
    public string Category => "Advanced";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            var status = _config.Enabled ? "enabled" : "disabled";
            return Task.FromResult(new SlashCommandResult(
                $"Swarm is currently {status}.\n\n" +
                "Usage:\n" +
                "  /swarm <prompt>     Run a swarm task (auto-enables if needed)\n" +
                "  /swarm <prompt> --dry-run  Show plan without executing\n" +
                "  /swarm enable       Enable swarm orchestration\n" +
                "  /swarm disable      Disable swarm orchestration\n" +
                "  /swarm yolo         Auto-accept all agent writes (no prompts)\n" +
                "  /swarm accept-edits Auto-accept file edits, prompt for bash\n" +
                "  /swarm ask          Prompt before every write (default)\n" +
                "  /swarm status       Show swarm configuration"));
        }

        // Enable/disable subcommands
        if (parts[0].Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            _config.Enabled = true;
            return Task.FromResult(new SlashCommandResult(
                "Swarm orchestration enabled for this session."));
        }

        if (parts[0].Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            _config.Enabled = false;
            return Task.FromResult(new SlashCommandResult(
                "Swarm orchestration disabled."));
        }

        if (parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new SlashCommandResult(
                $"Swarm: {(_config.Enabled ? "enabled" : "disabled")}\n" +
                string.Create(CultureInfo.InvariantCulture,
                    $"Max concurrent: {_config.MaxConcurrent}\n") +
                string.Create(CultureInfo.InvariantCulture,
                    $"Token budget: {_config.MaxTokenBudget:N0}\n") +
                string.Create(CultureInfo.InvariantCulture,
                    $"Quality gate: {(_config.QualityGateEnabled ? "on" : "off")}\n") +
                $"Permissions: {_config.Permissions}"));
        }

        if (parts[0].Equals("yolo", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "yolo";
            // If there's a prompt after "yolo", set permission AND run the task.
            if (parts.Length > 1)
            {
                if (!_config.Enabled) _config.Enabled = true;
                var yoloPrompt = string.Join(' ', parts.Skip(1).Where(p =>
                    !p.StartsWith("--", StringComparison.Ordinal)));
                var yoloDryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
                var yoloDryRunSuffix = yoloDryRun ? " Use dry_run mode to show the plan only." : string.Empty;
                return Task.FromResult(new SlashCommandResult(
                    InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                        $"Use the Swarm tool to execute: {yoloPrompt}{yoloDryRunSuffix}")));
            }
            return Task.FromResult(new SlashCommandResult(
                "Swarm permissions: YOLO — agents auto-accept all writes without prompting."));
        }

        if (parts[0].Equals("ask", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "ask";
            return Task.FromResult(new SlashCommandResult(
                "Swarm permissions: ask — agents prompt before writes."));
        }

        if (parts[0].Equals("accept-edits", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "accept-edits";
            // If there's a prompt after "accept-edits", set permission AND run the task.
            if (parts.Length > 1)
            {
                if (!_config.Enabled) _config.Enabled = true;
                var aePrompt = string.Join(' ', parts.Skip(1).Where(p =>
                    !p.StartsWith("--", StringComparison.Ordinal)));
                var aeDryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
                var aeDryRunSuffix = aeDryRun ? " Use dry_run mode to show the plan only." : string.Empty;
                return Task.FromResult(new SlashCommandResult(
                    InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                        $"Use the Swarm tool to execute: {aePrompt}{aeDryRunSuffix}")));
            }
            return Task.FromResult(new SlashCommandResult(
                "Swarm permissions: accept-edits — file writes auto-accepted, bash still prompts."));
        }

        // Otherwise treat as a swarm task prompt — auto-enable if needed.
        if (!_config.Enabled)
            _config.Enabled = true;

        var dryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

        var prompt = string.Join(' ', parts.Where(p =>
            !p.StartsWith("--", StringComparison.Ordinal)));

        if (string.IsNullOrWhiteSpace(prompt))
            return Task.FromResult(new SlashCommandResult("Error: no prompt provided."));

        var dryRunSuffix = dryRun ? " Use dry_run mode to show the plan only." : string.Empty;
        return Task.FromResult(new SlashCommandResult(
            InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                $"Use the Swarm tool to execute: {prompt}{dryRunSuffix}")));
    }
}
