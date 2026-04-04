using Sovrant.Runtime.Permissions;
using Sovrant.Server.ServerConfig;

namespace Sovrant.Server.Permissions;

/// <summary>
/// Implements <see cref="IPermissionModeAccessor"/> by reading and writing
/// <see cref="MutableServerConfig.PermissionMode"/>, so that EnterPlanMode and
/// ExitPlanMode tools work correctly in server mode.
/// </summary>
internal sealed class MutableServerPermissionModeAdapter : IPermissionModeAccessor
{
    private readonly MutableServerConfig _config;

    public MutableServerPermissionModeAdapter(MutableServerConfig config) => _config = config;

    /// <inheritdoc/>
    public PermissionMode Mode
    {
        get => _config.PermissionMode;
        set => _config.PermissionMode = value;
    }
}
