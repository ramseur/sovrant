namespace Sovrant.Commands;

/// <summary>
/// Routes user input beginning with <c>/</c> to the matching <see cref="ISlashCommand"/>.
/// </summary>
public sealed class SlashCommandDispatcher
{
    private readonly IReadOnlyDictionary<string, ISlashCommand> _byName;

    public SlashCommandDispatcher(IEnumerable<ISlashCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var dict = new Dictionary<string, ISlashCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var cmd in commands)
        {
            dict[cmd.Name] = cmd;
            foreach (var alias in cmd.Aliases)
                dict[alias] = cmd;
        }
        _byName = dict;
    }

    /// <summary>
    /// Tries to parse and dispatch <paramref name="input"/> as a slash command.
    /// Returns <c>null</c> if the input does not start with <c>/</c> or the command is unknown.
    /// </summary>
    public async Task<SlashCommandResult?> TryDispatchAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return null;

        var withoutSlash = input[1..].TrimStart();
        var spaceIdx = withoutSlash.IndexOf(' ', StringComparison.Ordinal);
        var name = spaceIdx < 0 ? withoutSlash : withoutSlash[..spaceIdx];
        var args = spaceIdx < 0 ? string.Empty : withoutSlash[(spaceIdx + 1)..].Trim();

        if (string.IsNullOrEmpty(name))
            return new SlashCommandResult("Type /help to see available commands.");

        if (!_byName.TryGetValue(name, out var cmd))
        {
            // Check for project-local command file: .sovrant/commands/{name}.md
            var projectCommandPath = Path.Combine(
                Directory.GetCurrentDirectory(), ".sovrant", "commands", $"{name}.md");
            if (File.Exists(projectCommandPath))
            {
                try
                {
                    var template = await File.ReadAllTextAsync(projectCommandPath, ct).ConfigureAwait(false);
                    // Substitute $ARGUMENTS placeholder if present
                    var injected = template.Contains("$ARGUMENTS", StringComparison.Ordinal)
                        ? template.Replace("$ARGUMENTS", args, StringComparison.Ordinal)
                        : string.IsNullOrEmpty(args) ? template : $"{template}\n\n{args}";
                    return new SlashCommandResult(InjectAsUserMessage: injected);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return new SlashCommandResult($"Error reading command file: {ex.Message}");
                }
            }

            return new SlashCommandResult($"Unknown command: /{name}. Type /help for a list of commands.");
        }

        return await cmd.ExecuteAsync(args, ct).ConfigureAwait(false);
    }

    /// <summary>Returns all registered commands (deduplicated — one entry per primary name).</summary>
    public IReadOnlyList<ISlashCommand> Commands =>
        _byName.Values.DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase).OrderBy(c => c.Name).ToList();
}
