namespace Sovrant.Runtime.Mcp;

/// <summary>Persistence layer for MCP trust gate rules.</summary>
public interface IMcpTrustRuleStore
{
    /// <summary>Returns all rules for the given workspace, optionally filtered by server name.</summary>
    Task<IReadOnlyList<McpTrustRule>> GetRulesAsync(
        string workspaceId,
        string? serverName = null,
        CancellationToken ct = default);

    /// <summary>Returns all rules across all workspaces for a specific server (used during evaluation).</summary>
    Task<IReadOnlyList<McpTrustRule>> GetRulesForServerAsync(
        string serverName,
        CancellationToken ct = default);

    /// <summary>Inserts or replaces a rule (keyed on rule_id).</summary>
    Task UpsertAsync(McpTrustRule rule, CancellationToken ct = default);

    /// <summary>Deletes a rule. Silently succeeds if the rule does not exist.</summary>
    Task DeleteAsync(string ruleId, string workspaceId, CancellationToken ct = default);

    /// <summary>Returns a single rule by ID, or null if not found.</summary>
    Task<McpTrustRule?> GetByIdAsync(string ruleId, string workspaceId, CancellationToken ct = default);
}
