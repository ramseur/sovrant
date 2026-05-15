using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>Defines a tool that the model can invoke.</summary>
public sealed record ToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("input_schema")] JsonElement InputSchema)
{
    /// <summary>Optional description of the tool's purpose.</summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
