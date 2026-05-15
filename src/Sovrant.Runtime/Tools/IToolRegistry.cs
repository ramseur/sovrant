using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.Tools;

/// <summary>A registry of available tools and their execution handlers.</summary>
public interface IToolRegistry
{
    /// <summary>Registers a tool definition and its execution handler.</summary>
    /// <param name="definition">The tool definition including name, description, and input schema.</param>
    /// <param name="handler">The function that executes the tool given its JSON input.</param>
    void Register(ToolDefinition definition, Func<JsonElement, CancellationToken, Task<string>> handler);

    /// <summary>Returns all registered tool definitions.</summary>
    IReadOnlyList<ToolDefinition> GetDefinitions();

    /// <summary>Attempts to retrieve the handler for the named tool.</summary>
    bool TryGetHandler(string name, out Func<JsonElement, CancellationToken, Task<string>>? handler);
}
