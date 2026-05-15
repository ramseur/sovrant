using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Permissions;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Permissions;

/// <summary>
/// An <see cref="IPermissionModeAccessor"/> that checks for a per-session override
/// before falling back to the global <see cref="MutableServerConfig"/>.
/// Uses <see cref="AsyncLocal{T}"/> to carry session context through the agentic loop.
/// </summary>
internal sealed class SessionAwarePermissionModeAdapter : IPermissionModeAccessor
{
    private readonly MutableServerConfig _globalConfig;

    public SessionAwarePermissionModeAdapter(MutableServerConfig globalConfig)
    {
        _globalConfig = globalConfig;
    }

    /// <summary>Sets the session config for the current async flow.</summary>
    public static void SetCurrent(SessionConfig? config) => SessionContext.Current = config;

    /// <inheritdoc/>
    public PermissionMode Mode
    {
        get => SessionContext.Current?.PermissionMode ?? _globalConfig.PermissionMode;
        set
        {
            if (SessionContext.Current is { } session)
                session.PermissionMode = value;
            else
                _globalConfig.PermissionMode = value;
        }
    }
}
