namespace Sovrant.Runtime.Hooks;

/// <summary>
/// Event-specific data passed to a hook command via template substitution
/// and <c>SOVRANT_HOOK_*</c> environment variables.
/// </summary>
public sealed record HookContext(
    HookEvent Event,
    string SessionId,
    string? ToolName = null,
    string? ToolInput = null,
    string? ToolOutput = null,
    string? FilePath = null,
    bool IsError = false,
    string? StopReason = null,
    int InputTokens = 0);
