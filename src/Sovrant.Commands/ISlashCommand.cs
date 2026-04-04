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

    /// <summary>
    /// Executes the command with the given arguments (everything after the command name).
    /// Returns <see cref="SlashCommandResult"/> describing what the REPL should do next.
    /// </summary>
    Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default);
}

/// <summary>The result of executing a slash command.</summary>
/// <param name="Output">Text to display to the user. Null means nothing to display.</param>
/// <param name="ShouldExit">When true the REPL should terminate.</param>
/// <param name="ShouldClearHistory">When true the REPL should clear conversation history.</param>
public sealed record SlashCommandResult(
    string? Output = null,
    bool ShouldExit = false,
    bool ShouldClearHistory = false);
