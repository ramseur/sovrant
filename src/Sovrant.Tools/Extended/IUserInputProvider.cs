namespace Sovrant.Tools.Extended;

/// <summary>
/// Provides a way to elicit user input mid-turn.
/// Implementations are provided by the CLI layer; the default returns a placeholder.
/// </summary>
public interface IUserInputProvider
{
    /// <summary>Presents <paramref name="question"/> to the user and returns their response.</summary>
    Task<string> AskAsync(string question, CancellationToken ct = default);
}

/// <summary>Default provider that returns a descriptive placeholder instead of blocking.</summary>
public sealed class NullUserInputProvider : IUserInputProvider
{
    public Task<string> AskAsync(string question, CancellationToken ct = default) =>
        Task.FromResult($"[User input requested: {question}]");
}
