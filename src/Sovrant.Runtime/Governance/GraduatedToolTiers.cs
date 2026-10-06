namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59c — static classification of all built-in tools into
/// <see cref="ToolTier"/> levels. Used by the permission policy, the
/// plan approval gate, and the step tool enforcer to make tier-aware
/// governance decisions.
/// <para>
/// Every tool registered by <c>AddSovrantTools</c> must have an entry here —
/// enforced by <c>GraduatedToolTiersCoverageTests</c> in Sovrant.Tools.Tests.
/// The legacy names <c>ReadFile</c>, <c>WriteFile</c>, <c>EditFile</c>,
/// <c>ListDirectory</c>, <c>List</c>, and <c>McpProxy</c> are not registered
/// tool names, but are kept because team allowed-tool lists and other
/// governance code still refer to them.
/// </para>
/// </summary>
public static class GraduatedToolTiers
{
    private static readonly Dictionary<string, ToolTier> Tiers = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Safe (read-only, no side effects) ───────────────────────
        ["ReadFile"]            = ToolTier.Safe,
        ["Read"]                = ToolTier.Safe,
        ["Glob"]                = ToolTier.Safe,
        ["Grep"]                = ToolTier.Safe,
        ["LS"]                  = ToolTier.Safe,
        ["ListDirectory"]       = ToolTier.Safe,
        ["List"]                = ToolTier.Safe,
        ["WebFetch"]            = ToolTier.Safe,
        ["WebSearch"]           = ToolTier.Safe,
        ["TaskGet"]             = ToolTier.Safe,
        ["TaskList"]            = ToolTier.Safe,
        ["TaskOutput"]          = ToolTier.Safe,
        ["LspHover"]            = ToolTier.Safe,
        ["LspDefinition"]       = ToolTier.Safe,
        ["LspReferences"]       = ToolTier.Safe,
        ["LspDiagnostics"]      = ToolTier.Safe,
        ["ToolSearch"]          = ToolTier.Safe,
        ["ListMcpResources"]    = ToolTier.Safe,
        ["ReadMcpResource"]     = ToolTier.Safe,
        ["Sleep"]               = ToolTier.Safe,
        ["AskUserQuestion"]     = ToolTier.Safe,
        ["EnterPlanMode"]       = ToolTier.Safe,
        ["ExitPlanMode"]        = ToolTier.Safe,
        ["CoordinationStatus"]  = ToolTier.Safe,
        ["CodeListTemplates"]   = ToolTier.Safe,
        ["CodeValidate"]        = ToolTier.Safe,
        ["DocumentListTemplates"]   = ToolTier.Safe,
        ["DocumentSuggestTemplate"] = ToolTier.Safe,
        ["DocumentListPackages"]    = ToolTier.Safe,

        // ── Moderate (writes/modifies state, but contained) ─────────
        ["WriteFile"]           = ToolTier.Moderate,
        ["Write"]               = ToolTier.Moderate,
        ["EditFile"]            = ToolTier.Moderate,
        ["Edit"]                = ToolTier.Moderate,
        ["NotebookEdit"]        = ToolTier.Moderate,
        ["TodoWrite"]           = ToolTier.Moderate,
        ["TaskCreate"]          = ToolTier.Moderate,
        ["TaskUpdate"]          = ToolTier.Moderate,
        ["TaskStop"]            = ToolTier.Moderate,
        ["Skill"]               = ToolTier.Moderate,
        ["SkillCreate"]         = ToolTier.Moderate,
        ["EnterWorktree"]       = ToolTier.Moderate,
        ["ExitWorktree"]        = ToolTier.Moderate,
        ["Verify"]              = ToolTier.Moderate,
        ["Artifact"]            = ToolTier.Moderate,
        ["MCPTool"]             = ToolTier.Moderate,
        ["McpProxy"]            = ToolTier.Moderate,
        ["McpAuth"]             = ToolTier.Moderate,
        ["LspRename"]           = ToolTier.Moderate,
        ["CodeCreate"]          = ToolTier.Moderate,
        ["CodeCreateMulti"]     = ToolTier.Moderate,
        ["DocumentGenerate"]    = ToolTier.Moderate,
        ["DocumentFromTemplate"] = ToolTier.Moderate,
        ["DocumentPackage"]     = ToolTier.Moderate,

        // ── Dangerous (arbitrary command execution) ─────────────────
        ["Bash"]                = ToolTier.Dangerous,
        ["PowerShell"]          = ToolTier.Dangerous,
        ["REPL"]                = ToolTier.Dangerous,

        // ── Escalation (delegation to other agents/systems) ─────────
        ["Agent"]               = ToolTier.Escalation,
        ["TeamCreate"]          = ToolTier.Escalation,
        ["TeamDelete"]          = ToolTier.Escalation,
        ["TeamStatus"]          = ToolTier.Escalation,
        ["TeamDelegate"]        = ToolTier.Escalation,
        ["TeamRun"]             = ToolTier.Escalation,
        ["TeamPublish"]         = ToolTier.Escalation,
        ["Workflow"]            = ToolTier.Escalation,
        ["Swarm"]               = ToolTier.Escalation,
        ["SwarmStatus"]         = ToolTier.Escalation,
    };

    /// <summary>
    /// Returns the tier for the given tool name. Unknown tools default to
    /// <see cref="ToolTier.Moderate"/> (safe assumption: treat unknowns
    /// as potentially modifying state).
    /// </summary>
    public static ToolTier GetTier(string toolName) =>
        Tiers.TryGetValue(toolName, out var tier) ? tier : ToolTier.Moderate;

    /// <summary>Returns whether the tool is at or above the given tier threshold.</summary>
    public static bool IsAtLeast(string toolName, ToolTier threshold) =>
        GetTier(toolName) >= threshold;

    /// <summary>Returns all known tool-to-tier mappings.</summary>
    public static IReadOnlyDictionary<string, ToolTier> All => Tiers;
}
