using System.Globalization;
using Sovrant.Runtime.Memory;

namespace Sovrant.Commands.Commands;

/// <summary>
/// Explicitly stores a learned pattern or instinct.
/// <list type="bullet">
///   <item><c>/remember pattern &lt;text&gt;</c> — saves a learned pattern for this project.</item>
///   <item><c>/remember instinct &lt;trigger&gt; | &lt;action&gt;</c> — saves a behavioral instinct.</item>
///   <item><c>/remember &lt;text&gt;</c> — shorthand for <c>/remember pattern &lt;text&gt;</c>.</item>
/// </list>
/// </summary>
public sealed class RememberCommand : ISlashCommand
{
    private readonly IMemoryStore _memoryStore;

    public RememberCommand(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    public string Name => "remember";
    public IReadOnlyList<string> Aliases => ["rem"];
    public string Description => "Save a learned pattern or instinct to memory.";

    public async Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        var arg = args.Trim();

        if (string.IsNullOrEmpty(arg))
            return new SlashCommandResult(
                "Usage:\n  /remember <text>                          — save a project pattern\n  /remember pattern <text>                  — save a project pattern\n  /remember instinct <trigger> | <action>   — save a behavioral instinct");

        if (arg.StartsWith("instinct ", StringComparison.OrdinalIgnoreCase))
        {
            return await SaveInstinctAsync(arg["instinct ".Length..].Trim(), ct).ConfigureAwait(false);
        }

        // Strip optional "pattern " prefix
        if (arg.StartsWith("pattern ", StringComparison.OrdinalIgnoreCase))
            arg = arg["pattern ".Length..].Trim();

        return await SavePatternAsync(arg, ct).ConfigureAwait(false);
    }

    private async Task<SlashCommandResult> SavePatternAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new SlashCommandResult("Pattern text cannot be empty.");

        var project = Directory.GetCurrentDirectory();
        var id = $"pattern-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";

        var pattern = new LearnedPattern
        {
            Id = id,
            Pattern = text,
            Project = project,
            Confidence = 0.7, // User-provided patterns start at higher confidence
        };

        await _memoryStore.SavePatternAsync(pattern, ct).ConfigureAwait(false);
        return new SlashCommandResult($"Saved pattern: {text}");
    }

    private async Task<SlashCommandResult> SaveInstinctAsync(string text, CancellationToken ct)
    {
        var parts = text.Split('|', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return new SlashCommandResult("Instinct format: /remember instinct <trigger> | <action>");

        var id = $"instinct-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";

        var instinct = new Instinct
        {
            Id = id,
            Trigger = parts[0],
            Action = parts[1],
            Confidence = 0.7, // User-provided instincts start at higher confidence
            Evidence = [$"{DateTimeOffset.UtcNow:yyyy-MM-dd}: Explicitly added by user"],
        };

        await _memoryStore.SaveInstinctAsync(instinct, ct).ConfigureAwait(false);
        return new SlashCommandResult($"Saved instinct: when \"{parts[0]}\" → {parts[1]}");
    }
}
