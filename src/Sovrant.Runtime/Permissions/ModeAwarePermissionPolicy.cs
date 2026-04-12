using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Implements permission policy based on the configured <see cref="PermissionMode"/>
/// and Phase 59c graduated <see cref="ToolTier"/>s.
/// <list type="bullet">
///   <item><b>Default</b> – Allow Safe tools; require confirmation for Moderate+ destructive tools.</item>
///   <item><b>AcceptEdits</b> – Allow Safe + Moderate; require confirmation for Dangerous+.</item>
///   <item><b>BypassPermissions</b> – Allow everything without prompting.</item>
///   <item><b>DontAsk</b> – Phase 59 fix: allow Safe + Moderate silently, require confirmation for
///     Dangerous tools, always show plan for Escalation. Set <c>SOVRANT_UNSAFE_DONTASK=true</c>
///     for truly unguarded mode (CI pipelines only).</item>
///   <item><b>Plan</b> – Block all destructive (write) tools outright.</item>
/// </list>
/// </summary>
public sealed class ModeAwarePermissionPolicy : IPermissionPolicy
{
    // Legacy sets kept for IsDestructive() backward compat.
    private static readonly HashSet<string> s_fileEditTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file", "edit_file", "create_file", "delete_file",
        "write", "edit", "create",
    };

    private static readonly HashSet<string> s_bashTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash", "shell", "run_command", "execute_command",
    };

    private readonly PermissionMode _mode;
    private readonly bool _unsafeDontAsk;

    public ModeAwarePermissionPolicy(PermissionMode mode)
    {
        _mode = mode;
        _unsafeDontAsk = string.Equals(
            Environment.GetEnvironmentVariable("SOVRANT_UNSAFE_DONTASK"),
            "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive)
    {
        var tier = GraduatedToolTiers.GetTier(toolName);

        return _mode switch
        {
            // BypassPermissions: always allow (admin mode).
            PermissionMode.BypassPermissions => PolicyDecision.Allow,

            // DontAsk (Phase 59 hardened):
            // - Safe + Moderate: allow silently
            // - Dangerous: require confirmation (unless SOVRANT_UNSAFE_DONTASK=true)
            // - Escalation: always require confirmation (plan must be shown)
            PermissionMode.DontAsk when _unsafeDontAsk => PolicyDecision.Allow,
            PermissionMode.DontAsk when tier == ToolTier.Escalation => PolicyDecision.RequireConfirmation,
            PermissionMode.DontAsk when tier == ToolTier.Dangerous => PolicyDecision.RequireConfirmation,
            PermissionMode.DontAsk => PolicyDecision.Allow,

            // Plan mode: block all destructive tools.
            PermissionMode.Plan when isDestructive => PolicyDecision.Deny,
            PermissionMode.Plan => PolicyDecision.Allow,

            // AcceptEdits: allow file edits (Moderate), prompt for Dangerous+.
            PermissionMode.AcceptEdits when tier <= ToolTier.Moderate => PolicyDecision.Allow,
            PermissionMode.AcceptEdits when isDestructive => PolicyDecision.RequireConfirmation,
            PermissionMode.AcceptEdits => PolicyDecision.Allow,

            // Default: allow Safe, prompt for destructive.
            _ when isDestructive => PolicyDecision.RequireConfirmation,
            _ => PolicyDecision.Allow,
        };
    }

    /// <summary>Returns whether the given tool name is considered destructive (writes or executes).</summary>
    public static bool IsDestructive(string toolName) =>
        s_fileEditTools.Contains(toolName) || s_bashTools.Contains(toolName);
}
