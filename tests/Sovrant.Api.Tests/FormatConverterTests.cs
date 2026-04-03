using System.Text.Json;
using Sovrant.Api.OpenAi;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>Tests for <see cref="FormatConverter"/>.</summary>
public sealed class FormatConverterTests
{
    [Fact]
    public void ToOpenAi_SimpleUserText_MapsToSingleUserMessage()
    {
        var req = new MessagesRequest("gpt-4o", 1024,
            [InputMessage.UserText("Hello")])
        {
            System = "You are helpful."
        };

        var result = FormatConverter.ToOpenAi(req);

        Assert.Equal("gpt-4o", result.Model);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("system", result.Messages[0].Role);
        Assert.Equal("You are helpful.", result.Messages[0].Content);
        Assert.Equal("user", result.Messages[1].Role);
        Assert.Equal("Hello", result.Messages[1].Content);
    }

    [Fact]
    public void ToOpenAi_AssistantWithToolUse_EmitsToolCalls()
    {
        var input = JsonDocument.Parse("{\"path\":\"/tmp/test\"}").RootElement;
        var req = new MessagesRequest("gpt-4o", 1024,
        [
            new InputMessage("assistant",
            [
                new InputContentBlock.TextBlock("Running tool."),
                new InputContentBlock.ToolUseBlock("tu_1", "bash", input)
            ])
        ]);

        var result = FormatConverter.ToOpenAi(req);

        Assert.Single(result.Messages);
        var msg = result.Messages[0];
        Assert.Equal("assistant", msg.Role);
        Assert.NotNull(msg.ToolCalls);
        Assert.Single(msg.ToolCalls!);
        Assert.Equal("tu_1", msg.ToolCalls![0].Id);
        Assert.Equal("bash", msg.ToolCalls[0].Function.Name);
    }

    [Fact]
    public void ToOpenAi_ToolResultBlock_BecomesToolRoleMessage()
    {
        var req = new MessagesRequest("gpt-4o", 1024,
        [
            new InputMessage("user",
            [
                new InputContentBlock.ToolResultBlock("tu_1",
                [new ToolResultContentBlock.TextBlock("exit 0")])
            ])
        ]);

        var result = FormatConverter.ToOpenAi(req);

        Assert.Single(result.Messages);
        Assert.Equal("tool", result.Messages[0].Role);
        Assert.Equal("tu_1", result.Messages[0].ToolCallId);
        Assert.Equal("exit 0", result.Messages[0].Content);
    }

    [Fact]
    public void FromOpenAi_TextChoice_ReturnsMessageResponseWithTextBlock()
    {
        var resp = new OpenAiChatResponse("id-1", "gpt-4o",
        [
            new OpenAiChoice(0,
                new OpenAiChoiceMessage("assistant", "Hello world"),
                "stop")
        ])
        { Usage = new OpenAiUsage(10, 5, 15) };

        var result = FormatConverter.FromOpenAi(resp);

        Assert.Equal("id-1", result.Id);
        Assert.Equal("gpt-4o", result.Model);
        Assert.Equal("end_turn", result.StopReason);
        var block = Assert.IsType<OutputContentBlock.TextBlock>(result.Content[0]);
        Assert.Equal("Hello world", block.Text);
        Assert.Equal(10, result.Usage.InputTokens);
        Assert.Equal(5, result.Usage.OutputTokens);
    }

    [Fact]
    public void FromOpenAi_ToolCallChoice_ReturnsToolUseBlock()
    {
        var resp = new OpenAiChatResponse("id-2", "gpt-4o",
        [
            new OpenAiChoice(0,
                new OpenAiChoiceMessage("assistant", null)
                {
                    ToolCalls =
                    [
                        new OpenAiToolCall("tc_1", "function",
                            new OpenAiFunction("bash", "{\"cmd\":\"ls\"}"))
                    ]
                },
                "tool_calls")
        ]);

        var result = FormatConverter.FromOpenAi(resp);

        Assert.Equal("tool_use", result.StopReason);
        var block = Assert.IsType<OutputContentBlock.ToolUseBlock>(result.Content[0]);
        Assert.Equal("tc_1", block.Id);
        Assert.Equal("bash", block.Name);
    }
}
