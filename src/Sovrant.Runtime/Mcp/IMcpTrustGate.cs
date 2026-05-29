namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Evaluates MCP tool calls against workspace trust rules before execution.
/// Returns a verdict that determines whether to allow, confirm, or block the call.
/// </summary>
public interface IMcpTrustGate
{
    /// <summary>
    /// Evaluates the trust policy for a tool call on a named MCP server.
    /// </summary>
    /// <param name="toolName">The tool being called.</param>
    /// <param name="serverName">The MCP server that owns the tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="McpTrustVerdict"/> indicating whether to allow, require confirmation, or block.
    /// </returns>
    Task<McpTrustVerdict> EvaluateAsync(
        string toolName,
        string serverName,
        CancellationToken ct = default);
}
