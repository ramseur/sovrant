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
        "Bash", "PowerShell", "REPL", "TaskCreate", // TaskCreate runs a shell command in the background
        // Git worktrees, code navigation and code tools that touch the disk or run commands
        "EnterWorktree", "ExitWorktree",
        "LspHover", "LspDefinition", "LspReferences", "LspDiagnostics", "LspRename",
        "CodeCreate", "CodeCreateMulti", "CodeValidate", "Verify",
    };

    public static bool TouchesHost(string toolName) => Tools.Contains(toolName);

    public const string HostBlockedMessage =
        "File and shell tools are turned off on this server, because they would run on the server itself. " +
        "An admin can turn them on under Governance → \"Allow file and shell tools on this server\".";

    public const string MemberBlockedMessage =
        "File and shell tools are turned off for members on this server, because they can reach other people's files. " +
        "An admin can turn them on under Governance → \"Let members use file and shell tools\".";
}

/// <summary>
/// Phase 145 Part D: on a hosted server (Web, Server), <see cref="HostToolAccess.Tools"/> are off for
/// everyone unless an admin turns on <see cref="GovernanceConfig.HostFileTools"/> — hosted services
/// don't run these on the server itself (Phase 147 brings code execution back in sandboxes). When it's
/// on, admins can use them and members also need <see cref="GovernanceConfig.MemberFileTools"/>.
/// </summary>
public sealed class MemberHostToolPolicy(IPrincipalAccessor principal, IWorkspaceSettingsStore? settings) : IHostToolPolicy
{
    // The two switches are read at most every few seconds: the tool list is filtered on every turn,
    // once per file/shell tool, and each read is a settings lookup.
    private static readonly TimeSpan SwitchesTtl = TimeSpan.FromSeconds(5);
    private readonly Lock _gate = new();
    private (bool Host, bool Members) _switches;
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;

    public string? GetBlockReason(string toolName)
    {
        if (!HostToolAccess.TouchesHost(toolName))
            return null;
        var (host, members) = Switches();
        if (!host)
            return HostToolAccess.HostBlockedMessage;
        if (principal.IsAdmin || members)
            return null;
        return HostToolAccess.MemberBlockedMessage;
    }

    private (bool Host, bool Members) Switches()
    {
        lock (_gate)
        {
            if (DateTimeOffset.UtcNow - _readAt < SwitchesTtl)
                return _switches;
            _switches = (
                WorkspaceSettingsResolver.ResolveBool(settings, WorkspaceSettingsKeys.GovernanceHostFileTools,
                    "SOVRANT_GOVERNANCE_HOST_FILE_TOOLS", fallback: false),
                WorkspaceSettingsResolver.ResolveBool(settings, WorkspaceSettingsKeys.GovernanceMemberFileTools,
                    "SOVRANT_GOVERNANCE_MEMBER_FILE_TOOLS", fallback: false));
            _readAt = DateTimeOffset.UtcNow;
            return _switches;
        }
    }
}

/// <summary>
/// Leaves tools the <see cref="IHostToolPolicy"/> blocks for the current person out of the tool list
/// the model sees, so it doesn't try them and fail. Calls still go through the executor, which refuses
/// them too.
/// </summary>
public sealed class HostPolicyToolRegistry(IToolRegistry inner, IHostToolPolicy policy) : IToolRegistry
{
    public void Register(Sovrant.Api.Types.ToolDefinition definition, Func<System.Text.Json.JsonElement, CancellationToken, Task<string>> handler) =>
        inner.Register(definition, handler);

    public IReadOnlyList<Sovrant.Api.Types.ToolDefinition> GetDefinitions() =>
        inner.GetDefinitions().Where(d => policy.GetBlockReason(d.Name) is null).ToList();

    public bool TryGetHandler(string name, out Func<System.Text.Json.JsonElement, CancellationToken, Task<string>>? handler) =>
        inner.TryGetHandler(name, out handler);
}

public static class HostToolPolicyExtensions
{
    /// <summary>
    /// Registers the hosted-server tool policy and hides blocked tools from the model. Call after the
    /// runtime is registered (it wraps the registered <see cref="IToolRegistry"/>).
    /// </summary>
    public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddHostToolPolicy(
        this Microsoft.Extensions.DependencyInjection.IServiceCollection services,
        Func<IServiceProvider, IHostToolPolicy> policy)
    {
        ArgumentNullException.ThrowIfNull(services);
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, policy);
        var existing = services.LastOrDefault(d => d.ServiceType == typeof(IToolRegistry))
            ?? throw new InvalidOperationException("Register the runtime (IToolRegistry) before AddHostToolPolicy.");
        services.Remove(existing);
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IToolRegistry>(services, sp =>
        {
            var inner = existing.ImplementationInstance as IToolRegistry
                ?? existing.ImplementationFactory?.Invoke(sp) as IToolRegistry
                ?? (IToolRegistry)Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(sp, existing.ImplementationType!);
            return new HostPolicyToolRegistry(inner, (IHostToolPolicy)sp.GetService(typeof(IHostToolPolicy))!);
        });
        return services;
    }
}
