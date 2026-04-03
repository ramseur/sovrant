namespace Sovrant.Runtime.Permissions;

/// <summary>Controls how the runtime handles potentially destructive tool invocations.</summary>
public enum PermissionMode
{
    /// <summary>Prompt the user before executing destructive operations.</summary>
    Default,

    /// <summary>Auto-accept file edits; prompt before bash and destructive operations.</summary>
    AcceptEdits,

    /// <summary>Allow all operations without prompting.</summary>
    BypassPermissions,

    /// <summary>Never prompt; silently allow everything.</summary>
    DontAsk,

    /// <summary>Read-only planning mode; all write operations are blocked.</summary>
    Plan
}
