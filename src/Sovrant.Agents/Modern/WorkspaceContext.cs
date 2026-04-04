using System.Collections.Concurrent;

namespace Sovrant.Agents.Modern;

/// <summary>
/// Shared, thread-safe scratch space for agents collaborating on a task. Holds named
/// variables, the current working directory, and any state that needs to be visible
/// across agent boundaries within a single multi-agent run.
/// </summary>
public sealed class WorkspaceContext
{
    private readonly ConcurrentDictionary<string, string> _state =
        new(StringComparer.Ordinal);

    /// <summary>
    /// The working directory for the current multi-agent run.
    /// Defaults to <see cref="Directory.GetCurrentDirectory()"/> at construction time.
    /// </summary>
    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>Gets a named workspace variable, or <see langword="null"/> if not set.</summary>
    public string? Get(string key) =>
        _state.TryGetValue(key, out var value) ? value : null;

    /// <summary>Sets a named workspace variable.</summary>
    public void Set(string key, string value) =>
        _state[key] = value;

    /// <summary>Removes a named workspace variable. Returns <see langword="true"/> if it existed.</summary>
    public bool Remove(string key) =>
        _state.TryRemove(key, out _);

    /// <summary>Returns a point-in-time snapshot of all current workspace variables.</summary>
    public IReadOnlyDictionary<string, string> Snapshot() =>
        new Dictionary<string, string>(_state, StringComparer.Ordinal);
}
