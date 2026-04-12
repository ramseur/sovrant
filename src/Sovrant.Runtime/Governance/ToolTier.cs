namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59c — graduated tool tiers used for permission governance.
/// Each tier represents an escalating level of risk/side-effect.
/// </summary>
public enum ToolTier
{
    /// <summary>Read-only tools with no side effects (Read, Glob, Grep, List).</summary>
    Safe,

    /// <summary>Tools that modify files or state but are contained (Write, Edit).</summary>
    Moderate,

    /// <summary>Tools that execute arbitrary commands (Bash, PowerShell, REPL).</summary>
    Dangerous,

    /// <summary>Tools that delegate to other agents/systems (Agent, Swarm, Mission).</summary>
    Escalation,
}
