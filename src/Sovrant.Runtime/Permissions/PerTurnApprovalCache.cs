namespace Sovrant.Runtime.Permissions;

/// <summary>
/// Phase 87 Track E — caches "always allow this turn" decisions so the user
/// does not get re-prompted for the same tool in the same conversation turn.
///
/// Scoping: an AsyncLocal holds the per-turn HashSet. <see cref="BeginTurn"/>
/// pushes a fresh set; the returned <see cref="IDisposable"/> pops it. This
/// keeps the cache turn-local without needing per-turn DI scopes.
/// </summary>
public interface IPerTurnApprovalCache
{
    /// <summary>Marks <paramref name="toolName"/> as approved for the rest of the current turn.</summary>
    void Allow(string toolName);

    /// <summary>Returns true if the tool was previously marked allowed for this turn.</summary>
    bool IsAllowed(string toolName);

    /// <summary>
    /// Begins a fresh approval scope for the current turn. Dispose to clear
    /// the scope. Nested scopes are stacked; the outer scope is restored on
    /// dispose.
    /// </summary>
    IDisposable BeginTurn();
}

/// <summary>Default in-memory implementation backed by AsyncLocal.</summary>
public sealed class PerTurnApprovalCache : IPerTurnApprovalCache
{
    private static readonly AsyncLocal<HashSet<string>?> s_current = new();

    public void Allow(string toolName)
    {
        var set = s_current.Value;
        if (set is null) return;
        set.Add(toolName);
    }

    public bool IsAllowed(string toolName)
    {
        var set = s_current.Value;
        return set is not null && set.Contains(toolName);
    }

    public IDisposable BeginTurn()
    {
        var prior = s_current.Value;
        s_current.Value = new HashSet<string>(StringComparer.Ordinal);
        return new Scope(prior);
    }

    private sealed class Scope : IDisposable
    {
        private readonly HashSet<string>? _prior;
        private bool _disposed;
        public Scope(HashSet<string>? prior) => _prior = prior;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_current.Value = _prior;
        }
    }
}
