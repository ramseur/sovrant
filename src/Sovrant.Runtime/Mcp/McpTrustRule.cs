namespace Sovrant.Runtime.Mcp;

/// <summary>Action to take when an MCP trust rule matches a tool call.</summary>
public enum McpTrustAction
{
    /// <summary>Permit the tool call without interruption.</summary>
    Allow,

    /// <summary>Pause and ask the user to approve before executing.</summary>
    RequireConfirmation,

    /// <summary>Refuse the tool call and return a structured error to the agent.</summary>
    Block,
}

/// <summary>
/// A workspace-scoped policy rule that governs how a specific MCP tool call pattern is handled.
/// </summary>
public sealed record McpTrustRule(
    string RuleId,
    string WorkspaceId,
    string ServerName,
    string ToolPattern,
    McpTrustAction Action,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Result of evaluating MCP trust rules for a tool call.</summary>
public sealed record McpTrustVerdict(
    McpTrustAction Action,
    string? Reason = null,
    string? MatchedPattern = null,
    bool IsBuiltIn = false);
