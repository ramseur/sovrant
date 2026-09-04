using System.Collections.Concurrent;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 67 — lookup for registered <see cref="IAutonomousDriver"/>s by
/// <see cref="IAutonomousDriver.Name"/>. Populated at DI-time from the
/// driver services the host has registered.
/// </summary>
public sealed class DriverRegistry
{
    private readonly ConcurrentDictionary<string, IAutonomousDriver> _drivers =
        new(StringComparer.OrdinalIgnoreCase);

    public DriverRegistry(IEnumerable<IAutonomousDriver> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        foreach (var d in drivers)
            _drivers[d.Name] = d;
    }

    /// <summary>All registered drivers, in insertion order is not guaranteed.</summary>
    public IReadOnlyCollection<IAutonomousDriver> All => _drivers.Values.ToArray();

    /// <summary>Returns the driver with the given name, or <see langword="null"/> if none is registered.</summary>
    public IAutonomousDriver? TryGet(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _drivers.GetValueOrDefault(name);
    }
}
