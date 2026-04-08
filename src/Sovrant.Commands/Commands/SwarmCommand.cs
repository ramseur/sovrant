using System.Globalization;
using Sovrant.Agents.Swarm;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Runs the swarm orchestrator from the REPL by injecting a user message
/// that triggers the Swarm tool. Also supports enable/disable subcommands.
/// Usage: <c>/swarm enable</c>, <c>/swarm disable</c>, <c>/swarm &lt;prompt&gt; [--dry-run]</c>,
/// or <c>/swarm --file &lt;path&gt; [--dry-run]</c> to load a master prompt from a file.
/// </summary>
public sealed class SwarmCommand : ISlashCommand
{
    private readonly SwarmConfig _config;

    public SwarmCommand(SwarmConfig config) => _config = config;

    public string Name => "swarm";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Enable/disable swarm or run a swarm task.";
    public string Category => "Advanced";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            var status = _config.Enabled ? "enabled" : "disabled";
            return new SlashCommandResult(
                $"Swarm is currently {status}.\n\n" +
                "Usage:\n" +
                "  /swarm <prompt>            Run a swarm task (auto-enables if needed)\n" +
                "  /swarm --file <path>       Load the master prompt from a .md/.txt file\n" +
                "  /swarm <prompt> --dry-run  Show plan without executing\n" +
                "  /swarm enable              Enable swarm orchestration\n" +
                "  /swarm disable             Disable swarm orchestration\n" +
                "  /swarm yolo                Auto-accept all agent writes (no prompts)\n" +
                "  /swarm accept-edits        Auto-accept file edits, prompt for bash\n" +
                "  /swarm ask                 Prompt before every write (default)\n" +
                "  /swarm status              Show swarm configuration");
        }

        // Enable/disable subcommands
        if (parts[0].Equals("enable", StringComparison.OrdinalIgnoreCase))
        {
            _config.Enabled = true;
            return new SlashCommandResult("Swarm orchestration enabled for this session.");
        }

        if (parts[0].Equals("disable", StringComparison.OrdinalIgnoreCase))
        {
            _config.Enabled = false;
            return new SlashCommandResult("Swarm orchestration disabled.");
        }

        if (parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return new SlashCommandResult(
                $"Swarm: {(_config.Enabled ? "enabled" : "disabled")}\n" +
                string.Create(CultureInfo.InvariantCulture,
                    $"Max concurrent: {_config.MaxConcurrent}\n") +
                string.Create(CultureInfo.InvariantCulture,
                    $"Token budget: {_config.MaxTokenBudget:N0}\n") +
                string.Create(CultureInfo.InvariantCulture,
                    $"Quality gate: {(_config.QualityGateEnabled ? "on" : "off")}\n") +
                $"Permissions: {_config.Permissions}");
        }

        if (parts[0].Equals("yolo", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "yolo";
            // If there's a prompt (or --file) after "yolo", set permission AND run the task.
            if (parts.Length > 1)
            {
                if (!_config.Enabled) _config.Enabled = true;
                var (yoloPrompt, yoloErr) = await ResolvePromptAsync(parts.Skip(1).ToArray(), ct).ConfigureAwait(false);
                if (yoloErr is not null) return new SlashCommandResult(yoloErr);
                var yoloDryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
                var yoloDryRunSuffix = yoloDryRun ? " Use dry_run mode to show the plan only." : string.Empty;
                return new SlashCommandResult(
                    InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                        $"Use the Swarm tool to execute: {yoloPrompt}{yoloDryRunSuffix}"));
            }
            return new SlashCommandResult(
                "Swarm permissions: YOLO — agents auto-accept all writes without prompting.");
        }

        if (parts[0].Equals("ask", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "ask";
            return new SlashCommandResult("Swarm permissions: ask — agents prompt before writes.");
        }

        if (parts[0].Equals("accept-edits", StringComparison.OrdinalIgnoreCase))
        {
            _config.Permissions = "accept-edits";
            // If there's a prompt (or --file) after "accept-edits", set permission AND run the task.
            if (parts.Length > 1)
            {
                if (!_config.Enabled) _config.Enabled = true;
                var (aePrompt, aeErr) = await ResolvePromptAsync(parts.Skip(1).ToArray(), ct).ConfigureAwait(false);
                if (aeErr is not null) return new SlashCommandResult(aeErr);
                var aeDryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
                var aeDryRunSuffix = aeDryRun ? " Use dry_run mode to show the plan only." : string.Empty;
                return new SlashCommandResult(
                    InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                        $"Use the Swarm tool to execute: {aePrompt}{aeDryRunSuffix}"));
            }
            return new SlashCommandResult(
                "Swarm permissions: accept-edits — file writes auto-accepted, bash still prompts.");
        }

        // Otherwise treat as a swarm task prompt — auto-enable if needed.
        if (!_config.Enabled)
            _config.Enabled = true;

        var dryRun = parts.Any(p => p.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));

        var (prompt, err) = await ResolvePromptAsync(parts, ct).ConfigureAwait(false);
        if (err is not null)
            return new SlashCommandResult(err);

        var dryRunSuffix = dryRun ? " Use dry_run mode to show the plan only." : string.Empty;
        return new SlashCommandResult(
            InjectAsUserMessage: string.Create(CultureInfo.InvariantCulture,
                $"Use the Swarm tool to execute: {prompt}{dryRunSuffix}"));
    }

    /// <summary>
    /// Pulls a master prompt out of the slash-command argv. If <c>--file</c> /
    /// <c>-f</c> is present, the next token is treated as a path and the file
    /// content becomes the prompt; otherwise the non-flag tokens are joined as
    /// an inline prompt. Returns <c>(prompt, null)</c> on success or
    /// <c>(null, errorMessage)</c> when something is wrong with the input.
    /// </summary>
    private static async Task<(string? Prompt, string? Error)> ResolvePromptAsync(
        string[] tokens,
        CancellationToken ct)
    {
        // Locate --file / -f and its argument.
        string? filePath = null;
        var inlineWords = new List<string>();
        for (var i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t.Equals("--file", StringComparison.OrdinalIgnoreCase) || t.Equals("-f", StringComparison.Ordinal))
            {
                if (i + 1 >= tokens.Length)
                    return (null, "Error: --file requires a path argument.");
                filePath = tokens[i + 1];
                i++; // skip the path token
                continue;
            }
            if (t.StartsWith("--", StringComparison.Ordinal))
                continue; // other flags (e.g. --dry-run) are handled by the caller
            inlineWords.Add(t);
        }

        var inline = string.Join(' ', inlineWords);

        if (filePath is not null && inlineWords.Count > 0)
            return (null, "Error: pass either an inline prompt or --file, not both.");

        if (filePath is not null)
        {
            try
            {
                var content = await SwarmPromptFile.LoadAsync(filePath, ct).ConfigureAwait(false);
                return (content, null);
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
            {
                return (null, "Error: " + ex.Message);
            }
        }

        if (string.IsNullOrWhiteSpace(inline))
            return (null, "Error: no prompt provided.");

        return (inline, null);
    }
}
