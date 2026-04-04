using Sovrant.Runtime.Permissions;

namespace Sovrant.Server.Permissions;

/// <summary>
/// An <see cref="IPermissionPolicy"/> that delegates to <see cref="ModeAwarePermissionPolicy"/>
/// using the live permission mode from the <see cref="IPermissionModeAccessor"/>,
/// which is session-aware (checks per-session override before global config).
/// </summary>
internal sealed class MutablePermissionPolicy : IPermissionPolicy
{
    private readonly IPermissionModeAccessor _accessor;

    public MutablePermissionPolicy(IPermissionModeAccessor accessor) => _accessor = accessor;

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive) =>
        new ModeAwarePermissionPolicy(_accessor.Mode).Evaluate(toolName, isDestructive);
}
