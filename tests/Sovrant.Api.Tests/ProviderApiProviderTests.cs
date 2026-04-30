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

    // ── Native server-tool injection (Anthropic web_search_20250305) ───────

    [Fact]
    public void AddAnthropicWebSearchServerTool_NoExistingTools_CreatesArrayWithServerTool()
    {
        var json = """{"model":"claude-opus-4-7","max_tokens":1024,"messages":[]}""";

        var patched = ProviderApiProvider.AddAnthropicWebSearchServerTool(json);

        using var doc = System.Text.Json.JsonDocument.Parse(patched);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        Assert.Equal("web_search_20250305", tools[0].GetProperty("type").GetString());
        Assert.Equal("web_search", tools[0].GetProperty("name").GetString());
    }

    [Fact]
    public void AddAnthropicWebSearchServerTool_ExistingTools_AppendsToArray()
    {
        var json = """{"model":"claude-opus-4-7","max_tokens":1024,"messages":[],"tools":[{"name":"Bash","description":"d","input_schema":{}}]}""";

        var patched = ProviderApiProvider.AddAnthropicWebSearchServerTool(json);

        using var doc = System.Text.Json.JsonDocument.Parse(patched);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.Equal(2, tools.GetArrayLength());
        Assert.Equal("Bash", tools[0].GetProperty("name").GetString());
        Assert.Equal("web_search_20250305", tools[1].GetProperty("type").GetString());
    }

    [Fact]
    public void AddAnthropicWebSearchServerTool_MalformedJson_ReturnsInputUnchanged()
    {
        var bad = "not json";

        var patched = ProviderApiProvider.AddAnthropicWebSearchServerTool(bad);

        Assert.Equal(bad, patched);
    }
}
