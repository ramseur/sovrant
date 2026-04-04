using System.Text.Json;
using Sovrant.Server.Webhooks;

namespace Sovrant.Server.Tests.Webhooks;

/// <summary>Tests for <see cref="WebhookRequest"/> serialization.</summary>
public sealed class WebhookRequestTests
{
    [Fact]
    public void Deserialize_AllFields()
    {
        var json = """
        {
            "source": "slack",
            "user_id": "U123",
            "message": "hello",
            "callback_url": "https://hooks.example.com/cb",
            "model": "gpt-4o",
            "thread_id": "t-1"
        }
        """;

        var req = JsonSerializer.Deserialize<WebhookRequest>(json)!;

        Assert.Equal("slack", req.Source);
        Assert.Equal("U123", req.UserId);
        Assert.Equal("hello", req.Message);
        Assert.Equal("https://hooks.example.com/cb", req.CallbackUrl);
        Assert.Equal("gpt-4o", req.Model);
        Assert.Equal("t-1", req.ThreadId);
    }

    [Fact]
    public void Deserialize_MinimalFields()
    {
        var json = """
        {
            "source": "discord",
            "user_id": "U456",
            "message": "hi"
        }
        """;

        var req = JsonSerializer.Deserialize<WebhookRequest>(json)!;

        Assert.Equal("discord", req.Source);
        Assert.Equal("U456", req.UserId);
        Assert.Equal("hi", req.Message);
        Assert.Null(req.CallbackUrl);
        Assert.Null(req.Model);
        Assert.Null(req.ThreadId);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var req = new WebhookRequest();

        Assert.Equal(string.Empty, req.Source);
        Assert.Equal(string.Empty, req.UserId);
        Assert.Equal(string.Empty, req.Message);
        Assert.Null(req.CallbackUrl);
    }
}
