using System.Text.Json;

namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Handles interactive user confirmation for tools that require it.
/// Implementations should prompt the user and return whether they approved.
/// </summary>
public interface IToolConfirmationHandler
{
    /// <summary>
    /// Asks the user whether to allow execution of the specified tool.
    /// </summary>
    /// <param name="toolName">The tool requesting confirmation.</param>
    /// <param name="input">The tool's input parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns><see langword="true"/> if the user approves; <see langword="false"/> to deny.</returns>
    Task<bool> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct);
}

/// <summary>
/// Default handler that denies all confirmation requests.
/// Used in non-interactive contexts (server, CI).
/// </summary>
public sealed class DenyAllConfirmationHandler : IToolConfirmationHandler
{
    public Task<bool> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
        => Task.FromResult(false);
}
