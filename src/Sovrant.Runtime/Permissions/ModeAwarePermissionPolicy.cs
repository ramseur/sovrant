namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Implements permission policy based on the configured <see cref="PermissionMode"/>.
/// <list type="bullet">
///   <item><b>Default</b> – Allow read tools; require confirmation for destructive tools.</item>
///   <item><b>AcceptEdits</b> – Allow all file edits; require confirmation for bash and other destructive ops.</item>
///   <item><b>BypassPermissions</b> – Allow everything without prompting.</item>
///   <item><b>DontAsk</b> – Allow everything silently.</item>
///   <item><b>Plan</b> – Block all destructive (write) tools outright.</item>
/// </list>
/// </summary>
public sealed class ModeAwarePermissionPolicy : IPermissionPolicy
{
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

    public ModeAwarePermissionPolicy(PermissionMode mode) => _mode = mode;

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive)
    {
        return _mode switch
        {
            PermissionMode.BypassPermissions or PermissionMode.DontAsk => PolicyDecision.Allow,

            PermissionMode.Plan when isDestructive => PolicyDecision.Deny,
            PermissionMode.Plan => PolicyDecision.Allow,

            PermissionMode.AcceptEdits when s_fileEditTools.Contains(toolName) => PolicyDecision.Allow,
            PermissionMode.AcceptEdits when isDestructive => PolicyDecision.RequireConfirmation,
            PermissionMode.AcceptEdits => PolicyDecision.Allow,

            // Default
            _ when isDestructive => PolicyDecision.RequireConfirmation,
            _ => PolicyDecision.Allow,
        };
    }

    /// <summary>Returns whether the given tool name is considered destructive (writes or executes).</summary>
    public static bool IsDestructive(string toolName) =>
        s_fileEditTools.Contains(toolName) || s_bashTools.Contains(toolName);
}
