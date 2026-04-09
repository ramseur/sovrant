using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Decorator that writes session entries to both SQLite (primary) and JSONL (secondary).
/// Activated when <c>SOVRANT_SESSION_JSONL=true</c>.
///
/// <para>Phase 38 — all reads go through the primary, which is authoritative
/// for ownership enforcement. Writes go to both. Deletes are gated on the
/// primary returning true (ownership match); the secondary is only touched
/// after a successful primary delete so JSONL files cannot be removed by a
/// non-owner.</para>
/// </summary>
internal sealed class DualWriteSessionStore(ISessionStore primary, ISessionStore secondary) : ISessionStore
{
    public async Task AppendAsync(
        string sessionId,
        SessionEntry entry,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        await primary.AppendAsync(sessionId, entry, ownerUserId, ct).ConfigureAwait(false);
        await secondary.AppendAsync(sessionId, entry, ownerUserId, ct).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SessionEntry>> LoadAsync(
        string sessionId,
        string? ownerUserId = null,
        CancellationToken ct = default)
        => primary.LoadAsync(sessionId, ownerUserId, ct);

    public Task<IReadOnlyList<string>> ListAsync(
        string? ownerUserId = null,
        CancellationToken ct = default)
        => primary.ListAsync(ownerUserId, ct);

    public async Task<bool> DeleteAsync(
        string sessionId,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        var deleted = await primary.DeleteAsync(sessionId, ownerUserId, ct).ConfigureAwait(false);
        // Only touch the secondary store if the primary accepted the delete
        // — otherwise a non-owner could scrub JSONL files they don't own.
        if (deleted)
            await secondary.DeleteAsync(sessionId, ownerUserId: null, ct).ConfigureAwait(false);
        return deleted;
    }

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        var count = await primary.DeleteAllAsync(ct).ConfigureAwait(false);
        await secondary.DeleteAllAsync(ct).ConfigureAwait(false);
        return count;
    }

    public Task<string?> GetOwnerAsync(string sessionId, CancellationToken ct = default)
        => primary.GetOwnerAsync(sessionId, ct);
}
