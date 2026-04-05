namespace Sovrant.McpServer;

/// <summary>
/// Filters which Sovrant tools are exposed via MCP based on the
/// <c>SOVRANT_MCP_TOOLS</c> environment variable (comma-separated allow-list).
/// When unset or empty, all tools are exposed.
/// </summary>
internal static class ToolFilter
{
    private const string EnvVar = "SOVRANT_MCP_TOOLS";

    /// <summary>
    /// Reads and parses the allow-list from the environment variable.
    /// Returns <see langword="null"/> if all tools should be allowed.
    /// </summary>
    internal static HashSet<string>? GetAllowList()
    {
        var raw = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            set.Add(part);
        }

        return set.Count == 0 ? null : set;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the tool should be exposed.
    /// The <c>chat</c> tool always passes (never filtered).
    /// </summary>
    internal static bool IsAllowed(string toolName, HashSet<string>? allowList)
    {
        if (string.Equals(toolName, ChatToolHandler.ToolName, StringComparison.Ordinal))
            return true;

        return allowList is null || allowList.Contains(toolName);
    }
}
