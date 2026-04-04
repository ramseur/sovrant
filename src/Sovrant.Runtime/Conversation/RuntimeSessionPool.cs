using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Routing;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;

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
    public async Task<IConversationRuntime> GetOrCreateAsync(
        string sessionId,
        ISmartRouter? scopedRouterOverride = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        // Fast path — already in pool.
        if (_pool.TryGetValue(sessionId, out var existing))
            return existing;

        // Slow path — create, initialise, and race to insert.
        IConversationRuntime runtime;
        if (scopedRouterOverride is not null)
        {
            // Build a runtime wired to the scoped router instead of the DI-registered one.
            runtime = new ConversationRuntime(
                scopedRouterOverride,
                _services.GetRequiredService<IToolExecutor>(),
                _services.GetRequiredService<IToolRegistry>(),
                _services.GetRequiredService<ISessionStore>(),
                _services.GetRequiredService<SovrantConfig>(),
                _services.GetRequiredService<ILogger<ConversationRuntime>>());
        }
        else
        {
            runtime = _services.GetRequiredService<IConversationRuntime>();
        }

        // Strip the composite key suffix for JSONL persistence — use only the original session ID.
        // Composite keys look like "session-id::provider-name". JSONL files use the raw session ID.
        var persistenceId = sessionId.Contains("::", StringComparison.Ordinal)
            ? sessionId[..sessionId.IndexOf("::", StringComparison.Ordinal)]
            : sessionId;

        await runtime.InitializeSessionAsync(persistenceId, ct).ConfigureAwait(false);

        // If another thread beat us, discard ours and return the winner.
        return _pool.GetOrAdd(sessionId, runtime);
    }

    /// <inheritdoc/>
    public void Evict(string sessionId) => _pool.TryRemove(sessionId, out _);
}
