namespace Sovrant.Runtime.Session;

/// <summary>
/// Lightweight summary of a session for listing in the UI.
/// </summary>
public sealed record SessionListItem(
    string SessionId,
    string? Title,
    DateTimeOffset UpdatedAt,
    string? OwnerUserId = null,
    string? WorkspaceId = null,
    bool IsPrivate = false);
