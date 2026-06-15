namespace Sovrant.Runtime.Knowledge;

/// <summary>
/// Phase 116 F — ambient context that tools read to record knowledge attributions
/// without needing an injected store dependency. Set by <see cref="ConversationRuntime"/>
/// at the start of each turn via <see cref="Begin"/>.
/// </summary>
public static class AttributionScope
{
    private static readonly AsyncLocal<AttributionContext?> s_current = new();

    public static AttributionContext? Current => s_current.Value;

    /// <summary>
    /// Sets the ambient attribution context for the current async execution context.
    /// The returned <see cref="IDisposable"/> restores the previous context on disposal,
    /// so nested scopes (e.g. sub-agents) correctly isolate their own attributions.
    /// </summary>
    public static IDisposable Begin(string sessionId, int turnIndex, IKnowledgeAttributionStore store)
    {
        var previous = s_current.Value;
        s_current.Value = new AttributionContext(sessionId, turnIndex, store);
        return new Restorer(previous);
    }

    private sealed class Restorer(AttributionContext? value) : IDisposable
    {
        public void Dispose() => s_current.Value = value;
    }
}

/// <summary>Captures session + turn identity for one attribution recording scope.</summary>
public sealed class AttributionContext(string sessionId, int turnIndex, IKnowledgeAttributionStore store)
{
    public Task RecordAsync(string kind, string slug, CancellationToken ct = default) =>
        store.RecordAsync(sessionId, turnIndex, kind, slug, ct);
}
