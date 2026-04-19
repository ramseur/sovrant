using System.Collections.Concurrent;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.Tools;

/// <summary>An in-memory <see cref="IToolRegistry"/> backed by a concurrent dictionary.</summary>
public sealed class InMemoryToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, (ToolDefinition Definition, Func<JsonElement, CancellationToken, Task<string>> Handler)>
        _tools = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Register(ToolDefinition definition, Func<JsonElement, CancellationToken, Task<string>> handler)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(handler);
        _tools[definition.Name] = (definition, handler);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> GetDefinitions() =>
        _tools.Values.Select(t => t.Definition).ToList();

    /// <inheritdoc/>
    public bool TryGetHandler(string name, out Func<JsonElement, CancellationToken, Task<string>>? handler)
    {
        if (_tools.TryGetValue(name, out var entry))
        {
            handler = entry.Handler;
            return true;
        }

        handler = null;
        return false;
    }
}
