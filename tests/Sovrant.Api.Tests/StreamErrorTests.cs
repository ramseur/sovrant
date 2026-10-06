using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Api.Tests;

/// <summary>
/// OpenRouter (and other OpenAI-compatible gateways) can answer HTTP 200 and then put the real
/// failure inside the SSE stream, e.g. a rate-limited :free model. These used to be skipped,
/// so the turn "completed" with an empty answer and the user had to send the prompt again.
/// </summary>
public sealed class StreamErrorTests
{
    [Fact]
    public void Reads_OpenRouter_Error_With_Code_And_Upstream_Detail()
    {
        const string data = """{"error":{"code":429,"message":"Provider returned error","metadata":{"raw":"free-models-per-min exceeded","provider_name":"Chutes"}}}""";
        Assert.Equal("Provider returned error 429: Provider returned error (free-models-per-min exceeded)",
            OpenAiCompatProvider.TryReadStreamError(data));
    }

    [Theory]
    [InlineData("""{"error":"upstream timeout"}""", "Provider returned error: upstream timeout")]
    [InlineData("""{"error":{"message":"overloaded"}}""", "Provider returned error: overloaded")]
    public void Reads_Other_Error_Shapes(string data, string expected)
    {
        Assert.Equal(expected, OpenAiCompatProvider.TryReadStreamError(data));
    }

    [Theory]
    [InlineData("""{"id":"x","choices":[{"index":0,"delta":{"content":"the word \"error\" in text"}}]}""")]
    [InlineData("""{"error":null,"choices":[]}""")]
    [InlineData("""not json "error" """)]
    public void Ordinary_Chunks_Are_Not_Errors(string data)
    {
        Assert.Null(OpenAiCompatProvider.TryReadStreamError(data));
    }

    [Fact]
    public async Task StreamAsync_Throws_The_Provider_Error_Instead_Of_Ending_Silently()
    {
        const string sse = "data: {\"error\":{\"code\":429,\"message\":\"Rate limit exceeded: free-models-per-min\"}}\n\ndata: [DONE]\n\n";
        var http = new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
        }))
        { BaseAddress = new Uri("https://openrouter.ai/api/v1/") };
        var provider = new OpenAiCompatProvider(http, new ApiKeyAuthProvider("k"), NullLogger.Instance);
        var request = new MessagesRequest("nvidia/nemotron-3-ultra-550b-a55b:free", 100, [new InputMessage("user", [new InputContentBlock.TextBlock("hi")])]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(request)) { }
        });
        Assert.Equal("Provider returned error 429: Rate limit exceeded: free-models-per-min", ex.Message);
    }
}
