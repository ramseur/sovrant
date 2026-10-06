using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tools;

/// <summary>
/// An optional check the tool executor runs before every tool call. Hosts that serve several
/// people from one machine (Sovrant.Web) register one; single-user hosts (Desktop, CLI) don't.
/// </summary>
public interface IHostToolPolicy
{
    /// <summary>A user-facing reason to refuse <paramref name="toolName"/>, or <c>null</c> to allow it.</summary>
    string? GetBlockReason(string toolName);
}

/// <summary>
/// Tools that read or write the server's disk or run processes on it. They run as the server's
/// OS account with no path limits (Phase 124 isn't built), so on a shared Web server any user's
/// agent could reach other users' files or the database.
/// </summary>
public static class HostToolAccess
{
    public static readonly IReadOnlySet<string> Tools = new HashSet<string>(StringComparer.Ordinal)
    {
        // Files
        "Read", "ReadFile", "Write", "WriteFile", "Edit", "EditFile", "NotebookEdit",
        "Glob", "Grep", "LS", "List", "ListDirectory",
        // Shell and processes
        "Bash", "PowerShell", "REPL",
        // Git worktrees, code navigation and code tools that touch the disk or run commands
        "EnterWorktree", "ExitWorktree",
        "LspHover", "LspDefinition", "LspReferences", "LspDiagnostics", "LspRename",
        "CodeCreate", "CodeCreateMulti", "CodeValidate", "Verify",
    };

    public static bool TouchesHost(string toolName) => Tools.Contains(toolName);

    public const string MemberBlockedMessage =
        "File and shell tools are turned off for members on this server, because they can reach other people's files. " +
        "An admin can turn them on under Governance → \"Let members use file and shell tools\".";
}

/// <summary>
/// Phase 145 stopgap (2.0.1): on a shared Web server, members (non-admins) can't use
/// <see cref="HostToolAccess.Tools"/> unless an admin has turned
/// <see cref="GovernanceConfig.MemberFileTools"/> on. Admins are never blocked.
/// </summary>
public sealed class MemberHostToolPolicy(IPrincipalAccessor principal, IWorkspaceSettingsStore? settings) : IHostToolPolicy
{
    public string? GetBlockReason(string toolName)
    {
        if (!HostToolAccess.TouchesHost(toolName) || principal.IsAdmin)
            return null;
        return GovernanceConfig.Load(settings).MemberFileTools ? null : HostToolAccess.MemberBlockedMessage;
    }
}
