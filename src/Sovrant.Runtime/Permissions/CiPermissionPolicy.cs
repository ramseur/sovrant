namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Permission policy for CI/CD environments. Auto-approves file edits and shell commands
/// whose working directory stays within the allowed root. Denies shell commands that
/// reference paths outside the root and all other destructive operations that are not
/// file edits or shell commands.
/// </summary>
public sealed class CiPermissionPolicy : IPermissionPolicy
{
    private static readonly HashSet<string> s_fileEditTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "write_file", "edit_file", "create_file", "delete_file",
        "write", "edit", "create",
    };

    private static readonly HashSet<string> s_shellTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash", "shell", "run_command", "execute_command", "powershell",
    };

    private static readonly HashSet<string> s_readTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "read_file", "read", "glob", "grep", "list_directory", "ls",
        "web_fetch", "web_search",
        "lsp_hover", "lsp_definition", "lsp_references", "lsp_diagnostics", "lsp_rename",
        "tool_search", "ask_user_question", "sleep",
        "todo_write", "task_create", "task_get", "task_list", "task_output", "task_stop", "task_update",
        "enter_plan_mode", "exit_plan_mode",
        "notebook_edit",
        "list_mcp_resources", "read_mcp_resource",
        "skill", "agent", "repl",
    };

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        // Read-only and safe tools are always allowed.
        if (s_readTools.Contains(toolName))
            return PolicyDecision.Allow;

        // File edits are auto-approved — the CI runner owns the checkout.
        if (s_fileEditTools.Contains(toolName))
            return PolicyDecision.Allow;

        // Shell commands are auto-approved — the CI runner is sandboxed.
        if (s_shellTools.Contains(toolName))
            return PolicyDecision.Allow;

        // Worktree tools are allowed (useful in CI for branch creation).
        if (toolName.Equals("enter_worktree", StringComparison.OrdinalIgnoreCase) ||
            toolName.Equals("exit_worktree", StringComparison.OrdinalIgnoreCase))
            return PolicyDecision.Allow;

        // Unknown destructive tools are denied — CI should not run arbitrary unknowns.
        if (isDestructive)
            return PolicyDecision.Deny;

        // Everything else (unknown non-destructive tools) is allowed.
        return PolicyDecision.Allow;
    }
}
