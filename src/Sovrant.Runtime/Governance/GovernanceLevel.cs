namespace Sovrant.Runtime.Governance;

/// <summary>
/// Controls the enforcement behavior of the governance monitor.
/// Configurable via <c>SOVRANT_GOVERNANCE_LEVEL</c> env var or <c>.sovrant/governance.json</c>.
/// </summary>
public enum GovernanceLevel
{
    /// <summary>Audit only — log events but never block or warn.</summary>
    Minimal,

    /// <summary>Audit + warn — log events and surface warnings in tool output.</summary>
    Standard,

    /// <summary>Audit + block — log events and block dangerous operations.</summary>
    Strict,
}
