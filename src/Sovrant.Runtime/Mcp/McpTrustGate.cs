using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Mcp;

/// <summary>
/// Evaluates MCP tool calls against stored workspace trust rules plus built-in defaults.
/// </summary>
/// <remarks>
/// Evaluation order:
/// <list type="number">
///   <item>Exact server match rules (most specific pattern wins — fewest wildcards).</item>
///   <item>Wildcard server ('*') rules.</item>
///   <item>Built-in defaults if no stored rule matched.</item>
/// </list>
/// Pattern matching supports a single '*' wildcard anywhere in the tool name string.
/// </remarks>
public sealed partial class McpTrustGate : IMcpTrustGate
{
    // Built-in conservative defaults — applied when no stored rule matches.
    // Covers the most common destructive verb prefixes/suffixes across MCP servers.
    private static readonly (string Pattern, McpTrustAction Action)[] BuiltInDefaults =
    [
        ("delete_*",   McpTrustAction.RequireConfirmation),
        ("*_delete",   McpTrustAction.RequireConfirmation),
        ("*_delete_*", McpTrustAction.RequireConfirmation),
        ("remove_*",   McpTrustAction.RequireConfirmation),
        ("*_remove",   McpTrustAction.RequireConfirmation),
        ("destroy_*",  McpTrustAction.RequireConfirmation),
        ("drop_*",     McpTrustAction.RequireConfirmation),
        ("purge_*",    McpTrustAction.RequireConfirmation),
        ("truncate_*", McpTrustAction.RequireConfirmation),
        ("bulk_*",     McpTrustAction.RequireConfirmation),
        ("*_bulk_*",   McpTrustAction.RequireConfirmation),
        ("batch_delete*", McpTrustAction.RequireConfirmation),
    ];

    private readonly IMcpTrustRuleStore _ruleStore;
    private readonly ILogger<McpTrustGate> _logger;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MCP trust gate: tool '{Tool}' on server '{Server}' → {Action} (pattern='{Pattern}', built-in={BuiltIn})")]
    private static partial void LogVerdict(ILogger logger, string tool, string server, McpTrustAction action, string pattern, bool builtIn);

    public McpTrustGate(IMcpTrustRuleStore ruleStore, ILogger<McpTrustGate> logger)
    {
        _ruleStore = ruleStore;
        _logger = logger;
    }

    public async Task<McpTrustVerdict> EvaluateAsync(
        string toolName,
        string serverName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolName);
        ArgumentException.ThrowIfNullOrEmpty(serverName);

        var rules = await _ruleStore.GetRulesForServerAsync(serverName, ct).ConfigureAwait(false);

        // Exact server rules first (server_name != '*'), then wildcard.
        // Within each group, prefer the most specific pattern (fewest wildcards).
        var ordered = rules
            .OrderBy(r => r.ServerName == "*" ? 1 : 0)
            .ThenBy(r => r.ToolPattern.Count(c => c == '*'));

        foreach (var rule in ordered)
        {
            if (!GlobMatch(rule.ToolPattern, toolName))
                continue;

            LogVerdict(_logger, toolName, serverName, rule.Action, rule.ToolPattern, false);
            return new McpTrustVerdict(rule.Action, rule.Reason, rule.ToolPattern, IsBuiltIn: false);
        }

        // Fall back to built-in defaults.
        foreach (var (pattern, action) in BuiltInDefaults)
        {
            if (!GlobMatch(pattern, toolName))
                continue;

            var reason = $"Tool name matches destructive pattern '{pattern}'. Explicit confirmation required.";
            LogVerdict(_logger, toolName, serverName, action, pattern, true);
            return new McpTrustVerdict(action, reason, pattern, IsBuiltIn: true);
        }

        return new McpTrustVerdict(McpTrustAction.Allow);
    }

    /// <summary>
    /// Simple glob match: '*' matches any sequence of characters (including empty).
    /// Case-insensitive. Only '*' is treated as a wildcard; '?' is treated literally.
    /// </summary>
    internal static bool GlobMatch(string pattern, string input)
    {
        if (pattern == "*") return true;

        var p = 0;
        var i = 0;
        var starP = -1;
        var starI = 0;

        while (i < input.Length)
        {
            if (p < pattern.Length &&
                (pattern[p] == '*' || char.ToLowerInvariant(pattern[p]) == char.ToLowerInvariant(input[i])))
            {
                if (pattern[p] == '*')
                {
                    starP = p++;
                    starI = i;
                }
                else
                {
                    p++;
                    i++;
                }
            }
            else if (starP != -1)
            {
                p = starP + 1;
                i = ++starI;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;

        return p == pattern.Length;
    }
}
