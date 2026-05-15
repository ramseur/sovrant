namespace Sovrant.Runtime.Permissions;

/// <summary>
/// An <see cref="IPermissionPolicy"/> whose mode can be changed at runtime by tools
/// such as <c>EnterPlanMode</c> and <c>ExitPlanMode</c>.
/// Used in the CLI context; the server overrides <see cref="IPermissionPolicy"/> and
/// <see cref="IPermissionModeAccessor"/> with its own mutable config-backed implementations.
/// </summary>
public sealed class MutableCliPermissionPolicy : IPermissionPolicy, IPermissionModeAccessor
{
    private volatile PermissionMode _mode;

    public MutableCliPermissionPolicy(PermissionMode initialMode) => _mode = initialMode;

    /// <inheritdoc/>
    public PermissionMode Mode
    {
        get => _mode;
        set => _mode = value;
    }

    /// <inheritdoc/>
    public PolicyDecision Evaluate(string toolName, bool isDestructive) =>
        new ModeAwarePermissionPolicy(_mode).Evaluate(toolName, isDestructive);
}
