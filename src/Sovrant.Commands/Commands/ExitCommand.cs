namespace Sovrant.Commands.Commands;

/// <summary>Exits the interactive REPL.</summary>
public sealed class ExitCommand : ISlashCommand
{
    public string Name => "exit";
    public IReadOnlyList<string> Aliases => ["quit", "q"];
    public string Description => "Exit the session.";

    public Task<SlashCommandResult> ExecuteAsync(string args, CancellationToken ct = default) =>
        Task.FromResult(new SlashCommandResult("Goodbye.", ShouldExit: true));
}
