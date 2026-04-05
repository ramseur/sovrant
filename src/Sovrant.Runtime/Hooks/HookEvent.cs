namespace Sovrant.Runtime.Hooks;

/// <summary>Lifecycle events at which hooks can be triggered.</summary>
public enum HookEvent
{
    /// <summary>Fired when a new session is created and initialized.</summary>
    SessionStart,

    /// <summary>
    /// Fired before a tool executes. Blocking in the <c>strict</c> profile —
    /// a non-zero hook exit code aborts the tool.
    /// </summary>
    PreToolUse,

    /// <summary>Fired after a tool completes without error. Fire-and-forget.</summary>
    PostToolUse,

    /// <summary>Fired after a tool fails (<c>IsError = true</c>). Fire-and-forget.</summary>
    PostToolUseFailure,

    /// <summary>Fired before context compaction begins. Awaited so state can be saved first.</summary>
    PreCompact,

    /// <summary>Fired after the agent emits <c>TurnComplete</c>. Fire-and-forget.</summary>
    Stop,

    /// <summary>Fired when a session is evicted from the pool. Fire-and-forget.</summary>
    SessionEnd,
}
