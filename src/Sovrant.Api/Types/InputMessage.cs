using System.Text.Json.Serialization;

namespace Sovrant.Api.Types;

/// <summary>A message in the conversation sent to the API.</summary>
public sealed record InputMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] IReadOnlyList<InputContentBlock> Content)
{
    /// <summary>Creates a user message containing a single text block.</summary>
    public static InputMessage UserText(string text) =>
        new("user", [new InputContentBlock.TextBlock(text)]);

    /// <summary>Creates a user message containing a tool result block.</summary>
    public static InputMessage UserToolResult(
        string toolUseId,
        IReadOnlyList<ToolResultContentBlock> content,
        bool isError = false) =>
        new("user", [new InputContentBlock.ToolResultBlock(toolUseId, content, isError)]);

    /// <summary>Creates an assistant message containing a single text block.</summary>
    public static InputMessage AssistantText(string text) =>
        new("assistant", [new InputContentBlock.TextBlock(text)]);
}
