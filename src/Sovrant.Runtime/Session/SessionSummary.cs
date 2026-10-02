namespace Sovrant.Runtime.Session;

/// <summary>
/// Lightweight summary of a session for listing in the UI.
/// </summary>
/// <param name="FolderId">Phase 133 — the conversation folder this session is filed in, or <c>null</c> when unfiled.</param>
/// <param name="AgentName">The agent currently attached to the session (V031), if any.</param>
/// <param name="Labels">
/// Phase 133 — sidebar labels derived from the session's live links (agent, workflow,
/// runs, webhook source). <c>null</c> until an <see cref="ISessionLinkResolver"/> fills them in.
/// </param>
public sealed record SessionListItem(
    string SessionId,
    string? Title,
    DateTimeOffset UpdatedAt,
    string? OwnerUserId = null,
    string? WorkspaceId = null,
    bool IsPrivate = false,
    string? FolderId = null,
    string? AgentName = null,
    IReadOnlyList<SessionLabel>? Labels = null);

/// <summary>
/// Phase 133 — one label on a conversation row, e.g. "Agent · researcher" or
/// "Workflow · Running". <see cref="IsActive"/> marks something running right
/// now (rendered with the live/ok color).
/// </summary>
public sealed record SessionLabel(string Text, bool IsActive = false);
