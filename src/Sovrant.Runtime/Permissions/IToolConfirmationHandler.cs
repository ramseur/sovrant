using System.Text.Json;

namespace Sovrant.Runtime.Permissions;

/// <summary>
/// The user's decision when a tool requests confirmation.
/// </summary>
public enum ConfirmationDecision
{
    /// <summary>The tool is denied for this call.</summary>
    Deny = 0,
    /// <summary>The tool is approved for this single call only.</summary>
    AllowOnce = 1,
    /// <summary>
    /// The tool is approved for this call AND every subsequent call to the
    /// same tool name within the current turn. Phase 87 Track E — used for
    /// "always allow Artifact for this turn" UI affordances.
    /// </summary>
    AllowForTurn = 2,
}

/// <summary>
/// Handles interactive user confirmation for tools that require it.
/// Implementations should prompt the user and return the user's decision.
/// </summary>
public interface IToolConfirmationHandler
{
    /// <summary>
    /// Asks the user whether to allow execution of the specified tool.
    /// </summary>
    /// <param name="toolName">The tool requesting confirmation.</param>
    /// <param name="input">The tool's input parameters.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>The user's decision.</returns>
    Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct);
}

/// <summary>
/// Default handler that denies all confirmation requests.
/// Used in non-interactive contexts (server, CI).
/// </summary>
public sealed class DenyAllConfirmationHandler : IToolConfirmationHandler
{
    public Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
        => Task.FromResult(ConfirmationDecision.Deny);
}
