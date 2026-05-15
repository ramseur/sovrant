namespace Sovrant.Runtime.Permissions;

/// <summary>Determines whether a tool invocation is allowed under the current permission configuration.</summary>
public interface IPermissionPolicy
{
    /// <summary>
    /// Returns <see langword="true"/> if the tool should be executed without prompting the user.
    /// Returns <see langword="false"/> if the tool is blocked outright (plan mode) or requires
    /// interactive confirmation (default mode for destructive tools).
    /// </summary>
    /// <param name="toolName">The name of the tool being invoked.</param>
    /// <param name="isDestructive">Whether the operation is considered destructive or write-oriented.</param>
    PolicyDecision Evaluate(string toolName, bool isDestructive);
}

/// <summary>The outcome of a permission policy evaluation.</summary>
public enum PolicyDecision
{
    /// <summary>The tool may execute without user confirmation.</summary>
    Allow,

    /// <summary>The user must confirm before the tool executes.</summary>
    RequireConfirmation,

    /// <summary>The tool is blocked and must not execute.</summary>
    Deny,
}
