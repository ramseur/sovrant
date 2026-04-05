using System.Text.Json;
using Sovrant.Server.Webhooks;

namespace Sovrant.Server.Tests.Webhooks;

/// <summary>Tests for webhook request/response serialization edge cases.</summary>
public sealed class SessionExportTests
{
    [Fact]
    public void WebhookToolCall_Defaults()
    {
        var tc = new WebhookToolCall();
        Assert.Equal(string.Empty, tc.Id);
        Assert.Equal(string.Empty, tc.ToolName);
        Assert.False(tc.IsError);
    }

    [Fact]
    public void WebhookToolCall_Serializes()
    {
        var tc = new WebhookToolCall { Id = "tc1", ToolName = "bash", IsError = true };
        var json = JsonSerializer.Serialize(tc);
        Assert.Contains("\"id\":\"tc1\"", json);
        Assert.Contains("\"tool_name\":\"bash\"", json);
        Assert.Contains("\"is_error\":true", json);
    }

    [Fact]
    public void WebhookRequest_WithAllOptionalFields()
    {
        var req = new WebhookRequest
        {
            Source = "custom",
            UserId = "u1",
            Message = "test",
            CallbackUrl = "https://example.com/cb",
            Model = "gpt-4o",
            ThreadId = "thread-1",
        };

        var json = JsonSerializer.Serialize(req);
        var rt = JsonSerializer.Deserialize<WebhookRequest>(json)!;

        Assert.Equal("custom", rt.Source);
        Assert.Equal("u1", rt.UserId);
        Assert.Equal("test", rt.Message);
        Assert.Equal("https://example.com/cb", rt.CallbackUrl);
        Assert.Equal("gpt-4o", rt.Model);
        Assert.Equal("thread-1", rt.ThreadId);
    }

    [Fact]
    public void WebhookResponse_ErrorState()
    {
        var response = new WebhookResponse
        {
            Success = false,
            Source = "slack",
            UserId = "U1",
            SessionId = "webhook:slack:U1",
            Text = "",
        };
        response.Errors.Add("runtime error");
        response.Errors.Add("tool error");

        Assert.False(response.Success);
        Assert.Equal(2, response.Errors.Count);
        Assert.Empty(response.ToolCalls);
    }
}
