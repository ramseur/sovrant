using Sovrant.Tools.Extended;

namespace Sovrant.Cli;

/// <summary>Reads user responses from the console when a tool requests mid-turn input.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI.")]
internal sealed class ConsoleUserInputProvider : IUserInputProvider
{
    /// <inheritdoc/>
    public Task<string> AskAsync(string question, CancellationToken ct = default)
    {
        Console.WriteLine();
        Console.Write($"[?] {question} ");
        var answer = Console.ReadLine() ?? string.Empty;
        return Task.FromResult(answer);
    }
}
