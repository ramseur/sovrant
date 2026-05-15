namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Singleton wrapper holding the current resolved snapshot of a config object.
/// Consumers either read <see cref="Current"/> on every call (cheap-rebuild
/// tier — scalars / lists) or subscribe to <see cref="Changed"/> to atomically
/// swap internal state (expensive-rebuild tier — compiled regexes, detector
/// graphs, etc.).
/// </summary>
/// <remarks>
/// Hot-reload semantics are <em>honour on next turn</em>: in-flight requests
/// keep their captured config; the next turn (or next agent task, or next
/// sanitizer call) sees the new values. We do not cancel or re-initialise
/// live sessions.
/// </remarks>
public interface ILiveSettings<T> where T : class
{
    /// <summary>The currently effective snapshot.</summary>
    T Current { get; }

    /// <summary>
    /// Subscribes <paramref name="handler"/> to be invoked after
    /// <see cref="Current"/> has been swapped to a new instance. Consumers in
    /// the expensive-rebuild tier (e.g. trust boundary detectors) call this
    /// to register a <c>Reconfigure</c> hook. Returns an <see cref="IDisposable"/>
    /// that unsubscribes when disposed.
    /// </summary>
    IDisposable OnChanged(Action<T> handler);
}

/// <summary>Static helpers for <see cref="ILiveSettings{T}"/>.</summary>
public static class LiveSettings
{
    /// <summary>
    /// Returns an <see cref="ILiveSettings{T}"/> that always reports
    /// <paramref name="value"/> and never raises change notifications. Useful
    /// for tests and for consumers that pre-date the live-settings infrastructure.
    /// </summary>
    public static ILiveSettings<T> Static<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return new StaticLiveSettings<T>(value);
    }

    private sealed class StaticLiveSettings<T>(T value) : ILiveSettings<T> where T : class
    {
        public T Current { get; } = value;
        public IDisposable OnChanged(Action<T> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            return EmptyDisposable.Instance;
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        public void Dispose() { }
    }
}

/// <summary>
/// Mutable wrapper used by the runtime for hot-reloadable configs. The
/// container registers one instance per setting class, and the
/// <see cref="LiveSettingsRegistry"/> drives synchronized reloads from the
/// Settings UI.
/// </summary>
public sealed class LiveSettings<T> : ILiveSettings<T> where T : class
{
    private readonly Func<T> _resolve;
    private readonly List<Action<T>> _handlers = [];
    private readonly Lock _gate = new();
    private T _current;

    /// <summary>
    /// Creates a wrapper that resolves the initial value via
    /// <paramref name="resolve"/>. The same delegate is invoked on
    /// <see cref="Reload"/> to produce the new snapshot.
    /// </summary>
    public LiveSettings(Func<T> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        _resolve = resolve;
        _current = resolve();
    }

    /// <inheritdoc />
    public T Current => Volatile.Read(ref _current)!;

    /// <inheritdoc />
    public IDisposable OnChanged(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate) _handlers.Add(handler);
        return new Subscription(this, handler);
    }

    /// <summary>
    /// Re-resolves and swaps <see cref="Current"/>, then notifies subscribers.
    /// Throwing handlers are caught so a single bad subscriber cannot block
    /// fan-out to the rest.
    /// </summary>
    public void Reload()
    {
        var fresh = _resolve();
        Volatile.Write(ref _current, fresh);

        Action<T>[] snapshot;
        lock (_gate) snapshot = [.. _handlers];
        foreach (var handler in snapshot)
        {
#pragma warning disable CA1031 // Intentional: one bad subscriber must not deny fan-out.
            try { handler(fresh); }
            catch { }
#pragma warning restore CA1031
        }
    }

    private void Unsubscribe(Action<T> handler)
    {
        lock (_gate) _handlers.Remove(handler);
    }

    private sealed class Subscription(LiveSettings<T> owner, Action<T> handler) : IDisposable
    {
        private LiveSettings<T>? _owner = owner;
        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Unsubscribe(handler);
        }
    }
}

/// <summary>
/// Coordinates fan-out reloads across every <see cref="LiveSettings{T}"/>
/// the container has registered. Settings UIs call <see cref="ReloadAll"/>
/// after persisting changes so the next runtime call sees the new values.
/// </summary>
public sealed class LiveSettingsRegistry
{
    private readonly List<Action> _reloaders = [];
    private readonly Lock _gate = new();

    /// <summary>Registers a wrapper for fan-out reloads.</summary>
    public void Register<T>(LiveSettings<T> wrapper) where T : class
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        lock (_gate) _reloaders.Add(wrapper.Reload);
    }

    /// <summary>Reloads every registered wrapper.</summary>
    public void ReloadAll()
    {
        Action[] snapshot;
        lock (_gate) snapshot = [.. _reloaders];
        foreach (var reload in snapshot)
            reload();
    }
}
