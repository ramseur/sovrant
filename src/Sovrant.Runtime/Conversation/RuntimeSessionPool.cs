using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Singleton pool that keeps one <see cref="IConversationRuntime"/> alive per session ID.
/// New runtimes are created via the DI container and initialised from persisted JSONL history
/// on first access, then reused for every subsequent request with the same session ID.
/// </summary>
internal sealed class RuntimeSessionPool : IRuntimeSessionPool
{
    private readonly IServiceProvider _services;
    private readonly ConcurrentDictionary<string, IConversationRuntime> _pool = new(StringComparer.Ordinal);

    public RuntimeSessionPool(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    public async Task<IConversationRuntime> GetOrCreateAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Fast path — already in pool.
        if (_pool.TryGetValue(sessionId, out var existing))
            return existing;

        // Slow path — create, initialise, and race to insert.
        var runtime = _services.GetRequiredService<IConversationRuntime>();
        await runtime.InitializeSessionAsync(sessionId, ct).ConfigureAwait(false);

        // If another thread beat us, discard ours and return the winner.
        return _pool.GetOrAdd(sessionId, runtime);
    }

    /// <inheritdoc/>
    public void Evict(string sessionId) => _pool.TryRemove(sessionId, out _);
}
