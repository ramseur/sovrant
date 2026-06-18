namespace Sovrant.Commands;

/// <summary>A slash command that can be invoked from the interactive REPL (e.g. /help, /clear).</summary>
public interface ISlashCommand
{
    /// <summary>Primary name of the command, without the leading slash (e.g. "help").</summary>
    string Name { get; }

    /// <summary>Optional alternate names (e.g. ["quit"] for the exit command).</summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>One-line description shown by /help.</summary>
    string Description { get; }

    /// <summary>Category for grouping in /help output (e.g. "Session", "Memory", "Config").</summary>
    string Category => "General";

    /// <summary>
    /// Executes the command with the given arguments (everything after the command name).
    /// Returns <see cref="SlashCommandResult"/> describing what the REPL should do next.
    /// </summary>
    Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default);

    /// <summary>
    /// Overload that carries caller identity. Defaults to ignoring <paramref name="ownerUserId"/>;
    /// commands that persist user-owned data (e.g. <c>/remember</c>) override this.
    /// </summary>
    Task<SlashCommandResult> ExecuteAsync(string args, string? ownerUserId, CancellationToken ct)
        => ExecuteAsync(args, ct);
}

/// <summary>The result of executing a slash command.</summary>
/// <param name="Output">Text to display to the user. Null means nothing to display.</param>
/// <param name="ShouldExit">When true the REPL should terminate.</param>
/// <param name="ShouldClearHistory">When true the REPL should clear conversation history.</param>
/// <param name="InjectAsUserMessage">
/// When non-null the REPL forwards this string to the LLM as a user message instead of
/// displaying it directly. Used by project slash commands loaded from
/// <c>.sovrant/commands/{name}.md</c>.
/// </param>
public sealed record SlashCommandResult(
    string? Output = null,
    bool ShouldExit = false,
    bool ShouldClearHistory = false,
    string? InjectAsUserMessage = null);
