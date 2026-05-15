using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.Tools;

/// <summary>
/// Decorator over <see cref="IToolRegistry"/> that restricts visibility to a set of allowed tools.
/// Tools not in the allowed set are hidden from <see cref="GetDefinitions"/> and blocked by
/// <see cref="TryGetHandler"/>.
/// </summary>
public sealed class FilteredToolRegistry : IToolRegistry
{
    private readonly IToolRegistry _inner;
    private readonly IReadOnlySet<string> _allowedTools;

    public FilteredToolRegistry(IToolRegistry inner, IReadOnlySet<string> allowedTools)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(allowedTools);
        _inner = inner;
        _allowedTools = allowedTools;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> GetDefinitions() =>
        _inner.GetDefinitions().Where(d => _allowedTools.Contains(d.Name)).ToList();

    /// <inheritdoc/>
    public bool TryGetHandler(string name, out Func<JsonElement, CancellationToken, Task<string>>? handler)
    {
        if (!_allowedTools.Contains(name))
        {
            handler = null;
            return false;
        }

        return _inner.TryGetHandler(name, out handler);
    }

    /// <inheritdoc/>
    public void Register(ToolDefinition definition, Func<JsonElement, CancellationToken, Task<string>> handler) =>
        _inner.Register(definition, handler);
}
