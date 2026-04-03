using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Tools;

/// <summary>A built-in tool that can be registered with the tool registry and invoked by the model.</summary>
public interface ITool
{
    /// <summary>The tool's definition including name, description, and input schema.</summary>
    ToolDefinition Definition { get; }

    /// <summary>Executes the tool with the given JSON input and returns its text output.</summary>
    Task<string> ExecuteAsync(JsonElement input, CancellationToken ct = default);
}
