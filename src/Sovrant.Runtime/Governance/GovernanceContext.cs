namespace Sovrant.Runtime.Governance;

/// <summary>Whether governance is evaluating a pre-execution or post-execution context.</summary>
public enum GovernancePhase
{
    /// <summary>Before the tool executes — can block.</summary>
    Pre,

    /// <summary>After the tool executes — can warn or audit.</summary>
    Post,
}

/// <summary>
/// Context passed to the governance monitor for evaluation.
/// Contains the tool name, input, output (post only), and session info.
/// </summary>
public sealed record GovernanceContext(
    GovernancePhase Phase,
    string ToolName,
    string? ToolInput = null,
    string? ToolOutput = null,
    string? SessionId = null,
    string? FilePath = null);
