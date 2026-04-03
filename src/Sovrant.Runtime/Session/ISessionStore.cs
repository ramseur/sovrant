namespace Sovrant.Runtime.Session;

/// <summary>Persists conversation session entries to durable storage.</summary>
public interface ISessionStore
{
    /// <summary>Appends a single entry to the session log.</summary>
    Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default);

    /// <summary>Loads all entries for the given session in order.</summary>
    Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Lists all session IDs that have at least one entry.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default);
}
