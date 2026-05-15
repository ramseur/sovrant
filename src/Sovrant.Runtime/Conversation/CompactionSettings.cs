using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Hot-reloadable wrapper for the compaction threshold scalar. Wrapped in
/// <see cref="ILiveSettings{T}"/> so the Settings UI can change the value
/// without requiring a process restart — <see cref="ConversationRuntime"/>
/// reads <see cref="Threshold"/> on each turn.
/// </summary>
/// <param name="Threshold">Input token count that triggers conversation auto-compaction. 0 disables.</param>
public sealed record CompactionSettings(int Threshold)
{
    /// <summary>
    /// Resolves via env (<c>SOVRANT_COMPACT_THRESHOLD</c>) &gt;
    /// <see cref="IWorkspaceSettingsStore"/> &gt; <paramref name="fallback"/>
    /// (the <c>settings.json</c> snapshot).
    /// </summary>
    public static CompactionSettings Resolve(IWorkspaceSettingsStore? settings, int fallback) =>
        new(WorkspaceSettingsResolver.ResolveInt(
            settings, WorkspaceSettingsKeys.CompactThreshold,
            "SOVRANT_COMPACT_THRESHOLD", fallback));
}
