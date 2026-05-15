using System.Globalization;
using System.Text;
using Sovrant.Runtime.Memory;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Removes memories from the multi-layered memory system.
/// <list type="bullet">
///   <item><c>/forget pattern &lt;id&gt;</c> — removes a learned pattern by ID.</item>
///   <item><c>/forget instinct &lt;id&gt;</c> — removes an instinct by ID.</item>
///   <item><c>/forget list</c> — lists all remembered patterns and instincts with their IDs.</item>
///   <item><c>/forget prune</c> — prunes low-confidence instincts.</item>
/// </list>
/// </summary>
public sealed class ForgetCommand : ISlashCommand
{
    private readonly IMemoryStore _memoryStore;

    public ForgetCommand(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    public string Name => "forget";
    public IReadOnlyList<string> Aliases => [];
    public string Description => "Remove or list memories (patterns, instincts).";
    public string Category => "Memory";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var arg = args.Trim();

        if (string.IsNullOrEmpty(arg))
            return new SlashCommandResult(
                "Usage:\n  /forget list                — list all patterns and instincts\n  /forget pattern <id>        — remove a learned pattern\n  /forget instinct <id>       — remove an instinct\n  /forget prune               — prune low-confidence instincts");

        if (arg.Equals("list", StringComparison.OrdinalIgnoreCase))
            return await ListAsync(ct).ConfigureAwait(false);

        if (arg.Equals("prune", StringComparison.OrdinalIgnoreCase))
            return await PruneAsync(ct).ConfigureAwait(false);

        if (arg.StartsWith("pattern ", StringComparison.OrdinalIgnoreCase))
        {
            var id = arg["pattern ".Length..].Trim();
            var project = Directory.GetCurrentDirectory();
            await _memoryStore.RemovePatternAsync(id, project, ct).ConfigureAwait(false);
            return new SlashCommandResult($"Removed pattern: {id}");
        }

        if (arg.StartsWith("instinct ", StringComparison.OrdinalIgnoreCase))
        {
            var id = arg["instinct ".Length..].Trim();
            await _memoryStore.RemoveInstinctAsync(id, ct).ConfigureAwait(false);
            return new SlashCommandResult($"Removed instinct: {id}");
        }

        return new SlashCommandResult($"Unknown subcommand: {arg}. Try /forget list.");
    }

    private async Task<SlashCommandResult> ListAsync(CancellationToken ct)
    {
        var project = Directory.GetCurrentDirectory();
        var patterns = await _memoryStore.LoadPatternsAsync(project, ct).ConfigureAwait(false);
        var instincts = await _memoryStore.LoadInstinctsAsync(0.0, ct).ConfigureAwait(false);

        if (patterns.Count == 0 && instincts.Count == 0)
            return new SlashCommandResult("No patterns or instincts stored.");

        var sb = new StringBuilder();

        if (patterns.Count > 0)
        {
            sb.AppendLine("── Learned Patterns ─────────────────────────");
            foreach (var p in patterns)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [{p.Id}] (conf: {p.Confidence:F1}) {p.Pattern}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        if (instincts.Count > 0)
        {
            sb.AppendLine("── Instincts ────────────────────────────────");
            foreach (var i in instincts)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [{i.Id}] (conf: {i.Confidence:F2}) {i.Trigger} → {i.Action}");
                sb.AppendLine();
            }
        }

        return new SlashCommandResult(sb.ToString().TrimEnd());
    }

    private async Task<SlashCommandResult> PruneAsync(CancellationToken ct)
    {
        var pruned = await _memoryStore.PruneInstinctsAsync(ct: ct).ConfigureAwait(false);
        return new SlashCommandResult(pruned > 0
            ? $"Pruned {pruned} low-confidence instinct(s)."
            : "No instincts to prune.");
    }
}
