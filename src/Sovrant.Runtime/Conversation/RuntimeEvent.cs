namespace Sovrant.Runtime.Conversation;

/// <summary>A discriminated union of events emitted by the conversation runtime during a turn.</summary>
public abstract record RuntimeEvent
{
    private RuntimeEvent() { }

    /// <summary>A streaming text chunk from the model.</summary>
    public sealed record TextChunk(string Text) : RuntimeEvent;

    /// <summary>The model has requested a tool invocation.</summary>
    public sealed record ToolUseRequested(
        string ToolUseId,
        string ToolName,
        System.Text.Json.JsonElement Input) : RuntimeEvent;

    /// <summary>A tool has been executed and its result is available.</summary>
    public sealed record ToolResult(
        string ToolUseId,
        string ToolName,
        string Content,
        bool IsError) : RuntimeEvent;

    /// <summary>The model turn is complete.</summary>
    public sealed record TurnComplete(
        string? StopReason,
        int InputTokens,
        int OutputTokens) : RuntimeEvent;

    /// <summary>A tool was denied by the permission policy.</summary>
    public sealed record PermissionDenied(
        string ToolName,
        string Reason) : RuntimeEvent;

    /// <summary>An error occurred during the turn.</summary>
    public sealed record RuntimeError(string Message) : RuntimeEvent;

    /// <summary>
    /// An MCP server requires OAuth authorization before it can be used.
    /// The authorization URL should be surfaced to the user so they can complete the flow.
    /// </summary>
    public sealed record AuthRequired(string ServerName, Uri AuthorizationUrl) : RuntimeEvent;
}
