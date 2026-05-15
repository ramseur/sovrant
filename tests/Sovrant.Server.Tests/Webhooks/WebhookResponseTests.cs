using System.Text.Json;
using Sovrant.Server.Webhooks;

namespace Sovrant.Server.Tests.Webhooks;

/// <summary>Tests for <see cref="WebhookResponse"/> serialization and structure.</summary>
public sealed class WebhookResponseTests
{
    [Fact]
    public void Serialize_ContainsAllFields()
    {
        var response = new WebhookResponse
        {
            Success = true,
            Source = "slack",
            UserId = "U123",
            ThreadId = "t-1",
            SessionId = "webhook:slack:U123",
            Text = "Hello!",
            InputTokens = 100,
            OutputTokens = 50,
        };
        response.ToolCalls.Add(new WebhookToolCall { Id = "tc1", ToolName = "read", IsError = false });
        response.Errors.Add("test error");

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"source\":\"slack\"", json);
        Assert.Contains("\"user_id\":\"U123\"", json);
        Assert.Contains("\"thread_id\":\"t-1\"", json);
        Assert.Contains("\"session_id\":\"webhook:slack:U123\"", json);
        Assert.Contains("\"text\":\"Hello!\"", json);
        Assert.Contains("\"input_tokens\":100", json);
        Assert.Contains("\"output_tokens\":50", json);
        Assert.Contains("\"tool_calls\"", json);
        Assert.Contains("\"tc1\"", json);
        Assert.Contains("\"errors\"", json);
        Assert.Contains("test error", json);
    }

    [Fact]
    public void ScalarFields_RoundTrip()
    {
        var response = new WebhookResponse
        {
            Success = true,
            Source = "teams",
            UserId = "U789",
            SessionId = "webhook:teams:U789",
            Text = "Done",
            InputTokens = 200,
            OutputTokens = 100,
        };

        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<WebhookResponse>(json)!;

        Assert.True(deserialized.Success);
        Assert.Equal("teams", deserialized.Source);
        Assert.Equal("U789", deserialized.UserId);
        Assert.Equal("webhook:teams:U789", deserialized.SessionId);
        Assert.Equal("Done", deserialized.Text);
        Assert.Equal(200, deserialized.InputTokens);
        Assert.Equal(100, deserialized.OutputTokens);
    }

    [Fact]
    public void Default_CollectionsAreEmpty()
    {
        var response = new WebhookResponse();

        Assert.Empty(response.ToolCalls);
        Assert.Empty(response.Errors);
        Assert.False(response.Success);
        Assert.Equal(string.Empty, response.Text);
    }

    [Fact]
    public void ToolCalls_CanAddMultiple()
    {
        var response = new WebhookResponse();
        response.ToolCalls.Add(new WebhookToolCall { Id = "1", ToolName = "read" });
        response.ToolCalls.Add(new WebhookToolCall { Id = "2", ToolName = "edit", IsError = true });

        Assert.Equal(2, response.ToolCalls.Count);
        Assert.False(response.ToolCalls[0].IsError);
        Assert.True(response.ToolCalls[1].IsError);
    }
}
