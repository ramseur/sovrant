using System.Text;
using System.Text.Json;
using Sovrant.Api.Types;

namespace Sovrant.Api.OpenAi;

/// <summary>Converts between internal Anthropic-format types and OpenAI chat completions format.</summary>
internal static class FormatConverter
{
    /// <summary>Converts a <see cref="MessagesRequest"/> to an <see cref="OpenAiChatRequest"/>.</summary>
    public static OpenAiChatRequest ToOpenAi(MessagesRequest req)
    {
        ArgumentNullException.ThrowIfNull(req);
        var messages = new List<OpenAiMessage>();
        if (req.System is not null)
        {
            messages.Add(new OpenAiMessage("system", req.System));
        }
        foreach (var msg in req.Messages)
        {
            ConvertInputMessage(msg, messages);
        }

        IReadOnlyList<OpenAiTool>? tools = null;
        if (req.Tools is { Count: > 0 })
        {
            tools = req.Tools
                .Select(t => new OpenAiTool("function", new OpenAiToolFunction(t.Name, t.Description, t.InputSchema)))
                .ToList();
        }

        return new OpenAiChatRequest(req.Model, req.MaxTokens, messages)
        {
            Tools = tools,
            Stream = req.Stream,
            StreamOptions = req.Stream ? new OpenAiStreamOptions(true) : null
        };
    }

    /// <summary>Converts an <see cref="OpenAiChatResponse"/> to a <see cref="MessageResponse"/>.</summary>
    public static MessageResponse FromOpenAi(OpenAiChatResponse resp)
    {
        ArgumentNullException.ThrowIfNull(resp);
        var choice = resp.Choices.Count > 0 ? resp.Choices[0] : null;
        var content = new List<OutputContentBlock>();

        if (choice?.Message.Content is { Length: > 0 } text)
        {
            content.Add(new OutputContentBlock.TextBlock(text));
        }
        if (choice?.Message.ToolCalls is { Count: > 0 } calls)
        {
            foreach (var call in calls)
            {
                var input = JsonDocument.Parse(
                    string.IsNullOrEmpty(call.Function.Arguments) ? "{}" : call.Function.Arguments
                ).RootElement;
                content.Add(new OutputContentBlock.ToolUseBlock(call.Id, call.Function.Name, input));
            }
        }

        var stopReason = choice?.FinishReason switch
        {
            "stop" => "end_turn",
            "tool_calls" => "tool_use",
            "length" => "max_tokens",
            var r => r
        };

        var usage = new Usage(
            InputTokens: resp.Usage?.PromptTokens ?? 0,
            OutputTokens: resp.Usage?.CompletionTokens ?? 0);

        return new MessageResponse(resp.Id, "message", "assistant", content, resp.Model, usage)
        {
            StopReason = stopReason
        };
    }

    private static void ConvertInputMessage(InputMessage msg, List<OpenAiMessage> output)
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
                            output.Add(new OpenAiMessage("user", string.Join("\n", textParts)));
                            textParts.Clear();
                        }
                        var resultText = string.Join("\n",
                            tr.Content.OfType<ToolResultContentBlock.TextBlock>().Select(x => x.Text));
                        output.Add(new OpenAiMessage("tool", resultText) { ToolCallId = tr.ToolUseId });
                        break;
                }
            }
            if (textParts.Count > 0)
            {
                output.Add(new OpenAiMessage("user", string.Join("\n", textParts)));
            }
        }
        else if (string.Equals(msg.Role, "assistant", StringComparison.Ordinal))
        {
            var textParts = new List<string>();
            var toolCalls = new List<OpenAiToolCall>();
            foreach (var block in msg.Content)
            {
                switch (block)
                {
                    case InputContentBlock.TextBlock t:
                        textParts.Add(t.Text);
                        break;
                    case InputContentBlock.ToolUseBlock tu:
                        toolCalls.Add(new OpenAiToolCall(tu.Id, "function",
                            new OpenAiFunction(tu.Name, tu.Input.GetRawText())));
                        break;
                }
            }
            output.Add(new OpenAiMessage("assistant", textParts.Count > 0 ? string.Join("", textParts) : null)
            {
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null
            });
        }
    }
}
