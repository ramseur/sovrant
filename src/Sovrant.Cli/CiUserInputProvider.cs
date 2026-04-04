using Sovrant.Tools.Extended;

namespace Sovrant.Cli;

/// <summary>
/// A no-op input provider for CI mode. There is no human at the terminal,
/// so any <c>AskUserQuestion</c> tool call returns an empty string.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI.")]
internal sealed class CiUserInputProvider : IUserInputProvider
{
    /// <inheritdoc/>
    public Task<string> AskAsync(string question, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);
}
