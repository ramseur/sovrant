namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Allows tools to read and change the active permission mode at runtime
/// without requiring a process restart.
/// </summary>
public interface IPermissionModeAccessor
{
    /// <summary>Gets or sets the currently active permission mode.</summary>
    PermissionMode Mode { get; set; }
}
