using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>Tests for <see cref="ProviderApiProvider"/>.</summary>
public sealed class ProviderApiProviderTests
{
    private const string ValidResponseJson = """
        {
          "id": "msg_01",
          "type": "message",
          "role": "assistant",
          "content": [{"type": "text", "text": "Hello"}],
          "model": "test-model",
          "stop_reason": "end_turn",
          "usage": {"input_tokens": 10, "output_tokens": 5}
        }
        """;

    private static ProviderApiProvider CreateProvider(System.Net.Http.HttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };
        return new ProviderApiProvider(http, new ApiKeyAuthProvider("test-key"),
            NullLogger<ProviderApiProvider>.Instance);
    }

    [Fact]
    public async Task SendAsync_Http200_ReturnsSuccessWithMessageResponse()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(ValidResponseJson)));
        var req = new MessagesRequest("test-model", 100, [InputMessage.UserText("Hi")]);

        var result = await provider.SendAsync(req);

        Assert.True(result.IsSuccess);
        Assert.Equal("msg_01", result.Value!.Id);
        Assert.Equal("end_turn", result.Value.StopReason);
        var block = Assert.IsType<OutputContentBlock.TextBlock>(result.Value.Content[0]);
        Assert.Equal("Hello", block.Text);
    }

    [Fact]
    public async Task SendAsync_Http401_ReturnsFailureResult()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.Unauthorized()));
        var req = new MessagesRequest("test-model", 100, [InputMessage.UserText("Hi")]);

        var result = await provider.SendAsync(req);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("401", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_NetworkError_ReturnsFailureResult()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            throw new HttpRequestException("connection refused"));
        var provider = CreateProvider(handler);
        var req = new MessagesRequest("test-model", 100, [InputMessage.UserText("Hi")]);

        var result = await provider.SendAsync(req);

        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsRetryable);
    }

    [Fact]
    public void Properties_ReturnExpectedValues()
    {
        var provider = CreateProvider(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk("{}")));
        Assert.Equal("provider-api", provider.Name);
        Assert.Contains("api.example.com", provider.BaseUrl.ToString(), StringComparison.Ordinal);
    }
}
