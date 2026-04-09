using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sovrant.Runtime.Conversation;

namespace Sovrant.McpServer;

/// <summary>
/// Handles the synthetic <c>chat</c> MCP tool that runs a full Sovrant agentic turn
/// (message → LLM → tool loop → response).
/// </summary>
internal static class ChatToolHandler
{
    internal const string ToolName = "chat";

    private static readonly JsonElement s_inputSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "message": {
              "type": "string",
              "description": "The user message to send to the agentic loop."
            },
            "session_id": {
              "type": "string",
              "description": "Optional session ID to resume a previous conversation."
            }
          },
          "required": ["message"]
        }
        """).RootElement.Clone();

    internal static Tool Definition { get; } = new()
    {
        Name = ToolName,
        Description = "Run a full Sovrant agentic turn: sends a message through the LLM with tool use, returning the final response.",
        InputSchema = s_inputSchema,
    };

    internal static async Task<CallToolResult> ExecuteAsync(
        IServiceProvider services,
        IDictionary<string, JsonElement>? arguments,
        CancellationToken ct)
    {
        var message = arguments is not null
            && arguments.TryGetValue("message", out var msgElem)
            && msgElem.ValueKind == JsonValueKind.String
            ? msgElem.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = "The 'message' parameter is required." }],
            };
        }

        var sessionId = arguments is not null
            && arguments.TryGetValue("session_id", out var sidElem)
            && sidElem.ValueKind == JsonValueKind.String
            ? sidElem.GetString()
            : null;

        var runtime = services.GetRequiredService<IConversationRuntime>();

        if (sessionId is not null)
            await runtime.InitializeSessionAsync(sessionId, ownerUserId: null, ct).ConfigureAwait(false);

        var textBuilder = new StringBuilder();
        var toolSummaries = new List<string>();

        await foreach (var evt in runtime.RunTurnAsync(message, ct).ConfigureAwait(false))
        {
            switch (evt)
            {
                case RuntimeEvent.TextChunk { Text: var text }:
                    textBuilder.Append(text);
                    break;

                case RuntimeEvent.ToolResult { ToolName: var tool, IsError: var isErr }:
                    toolSummaries.Add(isErr ? $"[{tool}: error]" : $"[{tool}: ok]");
                    break;

                case RuntimeEvent.RuntimeError { Message: var errMsg }:
                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = errMsg }],
                    };
            }
        }

        var result = textBuilder.ToString();
        if (toolSummaries.Count > 0)
        {
            result += Environment.NewLine + Environment.NewLine
                + "Tools used: " + string.Join(", ", toolSummaries);
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result }],
        };
    }
}
