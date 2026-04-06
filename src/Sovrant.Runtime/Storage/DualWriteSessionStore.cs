using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Decorator that writes session entries to both SQLite (primary) and JSONL (secondary).
/// Activated when <c>SOVRANT_SESSION_JSONL=true</c>.
/// </summary>
internal sealed class DualWriteSessionStore(ISessionStore primary, ISessionStore secondary) : ISessionStore
{
    public async Task AppendAsync(string sessionId, SessionEntry entry, CancellationToken ct = default)
    {
        await primary.AppendAsync(sessionId, entry, ct).ConfigureAwait(false);
        await secondary.AppendAsync(sessionId, entry, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SessionEntry>> LoadAsync(string sessionId, CancellationToken ct = default)
        => primary.LoadAsync(sessionId, ct);

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
        => primary.ListAsync(ct);
}
