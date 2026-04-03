using Sovrant.Runtime.Tools;

namespace Sovrant.Tools;

/// <summary>Registers all <see cref="ITool"/> implementations into the runtime <see cref="IToolRegistry"/>.</summary>
public sealed class ToolRegistrar
{
    private readonly IEnumerable<ITool> _tools;
    private readonly IToolRegistry _registry;

    public ToolRegistrar(IEnumerable<ITool> tools, IToolRegistry registry)
    {
        _tools = tools;
        _registry = registry;
    }

    /// <summary>Registers every discovered <see cref="ITool"/> into the registry.</summary>
    public void RegisterAll()
    {
        foreach (var tool in _tools)
            _registry.Register(tool.Definition, tool.ExecuteAsync);
    }
}
