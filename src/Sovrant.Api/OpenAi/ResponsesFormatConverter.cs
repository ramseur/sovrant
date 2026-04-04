using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Api.OpenAi;

/// <summary>Converts between Sovrant's internal message types and the OpenAI Responses API format.</summary>
internal static class ResponsesFormatConverter
{
    /// <summary>Converts a <see cref="MessagesRequest"/> to an <see cref="OpenAiResponsesRequest"/>.</summary>
    public static OpenAiResponsesRequest ToResponses(MessagesRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);

        var input = new List<OpenAiResponsesInputItem>();
        foreach (var msg in req.Messages)
            ConvertInputMessage(msg, input);

        // Include function tools (excluding WebSearch — web_search_preview replaces it)
        // then append the native web_search_preview built-in tool.
        var tools = new List<OpenAiResponsesTool>();
        if (req.Tools is { Count: > 0 })
        {
            foreach (var t in req.Tools)
            {
                if (!string.Equals(t.Name, "WebSearch", StringComparison.Ordinal))
                    tools.Add(new OpenAiResponsesTool("function", t.Name, t.Description, t.InputSchema));
            }
        }
        tools.Add(new OpenAiResponsesTool("web_search_preview"));

        return new OpenAiResponsesRequest(req.Model, input)
        {
            Instructions = req.System,
            Tools = tools,
            Stream = req.Stream,
            MaxOutputTokens = req.MaxTokens > 0 ? req.MaxTokens : null
        };
    }

    /// <summary>Converts a completed <see cref="OpenAiResponsesResponse"/> to a <see cref="MessageResponse"/>.</summary>
    public static MessageResponse FromResponses(OpenAiResponsesResponse resp)
    {
        ArgumentNullException.ThrowIfNull(resp);

        var content = new List<OutputContentBlock>();
        foreach (var item in resp.Output ?? [])
        {
            if (string.Equals(item.Type, "message", StringComparison.Ordinal) && item.Content is not null)
            {
                foreach (var part in item.Content)
                {
                    if (string.Equals(part.Type, "output_text", StringComparison.Ordinal) && part.Text is not null)
                        content.Add(new OutputContentBlock.TextBlock(part.Text));
                }
            }
            else if (string.Equals(item.Type, "function_call", StringComparison.Ordinal) && item.Name is not null)
            {
                var argsJson = string.IsNullOrEmpty(item.Arguments) ? "{}" : item.Arguments;
                var inputEl = JsonDocument.Parse(argsJson).RootElement;
                // Prefer CallId for Responses API; fall back to Id
                var toolUseId = item.CallId ?? item.Id ?? string.Empty;
                content.Add(new OutputContentBlock.ToolUseBlock(toolUseId, item.Name, inputEl));
            }
            // web_search_call items are informational — the model handles them server-side
        }

        var hasToolUse = content.Exists(c => c is OutputContentBlock.ToolUseBlock);
        var stopReason = hasToolUse ? "tool_use" : "end_turn";
        var usage = new Usage(
            InputTokens: resp.Usage?.InputTokens ?? 0,
            OutputTokens: resp.Usage?.OutputTokens ?? 0);

        return new MessageResponse(resp.Id, "message", "assistant", content, resp.Model ?? string.Empty, usage)
        {
            StopReason = stopReason
        };
    }

    private static void ConvertInputMessage(InputMessage msg, List<OpenAiResponsesInputItem> output)
    {
        if (string.Equals(msg.Role, "user", StringComparison.Ordinal))
        {
            var textParts = new List<string>();
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case InputContentBlock.TextBlock t:
                        textParts.Add(t.Text);
                        break;
                    case InputContentBlock.ToolResultBlock tr:
                        if (textParts.Count > 0)
                        {
                            output.Add(new OpenAiResponsesInputItem(
                                null, "user", string.Join("\n", textParts), null, null, null, null, null));
                            textParts.Clear();
                        }
                        var resultText = string.Join("\n",
                            tr.Content.OfType<ToolResultContentBlock.TextBlock>().Select(x => x.Text));
                        output.Add(new OpenAiResponsesInputItem(
                            "function_call_output", null, null, null, tr.ToolUseId, null, null, resultText));
                        break;
                }
            }
            if (textParts.Count > 0)
                output.Add(new OpenAiResponsesInputItem(
                    null, "user", string.Join("\n", textParts), null, null, null, null, null));
        }
        else if (string.Equals(msg.Role, "assistant", StringComparison.Ordinal))
        {
            var textParts = new List<string>();
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case InputContentBlock.TextBlock t:
                        textParts.Add(t.Text);
                        break;
                    case InputContentBlock.ToolUseBlock tu:
                        if (textParts.Count > 0)
                        {
                            output.Add(new OpenAiResponsesInputItem(
                                null, "assistant", string.Join("\n", textParts), null, null, null, null, null));
                            textParts.Clear();
                        }
                        // The Responses API requires function_call.id to start with 'fc' (item ID).
                        // tu.Id stores the call_id (starts with "call_"), so we derive a synthetic item ID.
                        // function_call.call_id = tu.Id matches function_call_output.call_id = tr.ToolUseId.
                        var fcItemId = tu.Id.StartsWith("fc", StringComparison.Ordinal) ? tu.Id : "fc_" + tu.Id;
                        output.Add(new OpenAiResponsesInputItem(
                            "function_call", null, null, fcItemId, tu.Id, tu.Name, tu.Input.GetRawText(), null));
                        break;
                }
            }
            if (textParts.Count > 0)
                output.Add(new OpenAiResponsesInputItem(
                    null, "assistant", string.Join("\n", textParts), null, null, null, null, null));
        }
    }
}
