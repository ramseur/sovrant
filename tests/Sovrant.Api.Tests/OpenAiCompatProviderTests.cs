using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>Tests for <see cref="OpenAiCompatProvider"/>.</summary>
public sealed class OpenAiCompatProviderTests
{
    private const string TextResponseJson = """
        {
          "id": "chatcmpl-1",
          "model": "gpt-4o",
          "choices": [{
            "index": 0,
            "message": {"role": "assistant", "content": "2 + 2 = 4"},
            "finish_reason": "stop"
          }],
          "usage": {"prompt_tokens": 8, "completion_tokens": 6, "total_tokens": 14}
        }
        """;

    private const string ToolCallResponseJson = """
        {
          "id": "chatcmpl-2",
          "model": "gpt-4o",
          "choices": [{
            "index": 0,
            "message": {
              "role": "assistant",
              "content": null,
              "tool_calls": [{
                "id": "call_1",
                "type": "function",
                "function": {"name": "bash", "arguments": "{\"cmd\":\"ls\"}"}
              }]
            },
            "finish_reason": "tool_calls"
          }]
        }
        """;

    private static OpenAiCompatProvider CreateProvider(System.Net.Http.HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com") };
        ILogger logger = NullLogger.Instance;
        return new OpenAiCompatProvider(http, new ApiKeyAuthProvider("sk-test"), logger);
    }

    [Fact]
    public async Task SendAsync_TextResponse_ReturnsMappedMessageResponse()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(TextResponseJson)));
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("What is 2+2?")]);

        var result = await provider.SendAsync(req);

        Assert.True(result.IsSuccess);
        Assert.Equal("end_turn", result.Value!.StopReason);
        var block = Assert.IsType<OutputContentBlock.TextBlock>(result.Value.Content[0]);
        Assert.Equal("2 + 2 = 4", block.Text);
    }

    [Fact]
    public async Task SendAsync_ToolCallResponse_ReturnsMappedToolUseBlock()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(ToolCallResponseJson)));
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Run ls")]);

        var result = await provider.SendAsync(req);

        Assert.True(result.IsSuccess);
        Assert.Equal("tool_use", result.Value!.StopReason);
        var block = Assert.IsType<OutputContentBlock.ToolUseBlock>(result.Value.Content[0]);
        Assert.Equal("call_1", block.Id);
        Assert.Equal("bash", block.Name);
    }

    [Fact]
    public async Task SendAsync_Http401_ReturnsFailureResult()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.Unauthorized()));
        var req = new MessagesRequest("gpt-4o", 100, [InputMessage.UserText("Hi")]);

        var result = await provider.SendAsync(req);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Properties_ReturnExpectedValues()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        Assert.Equal("openai-compat", provider.Name);
    }
}
