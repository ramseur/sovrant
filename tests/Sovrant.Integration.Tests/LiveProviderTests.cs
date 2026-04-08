using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;

namespace Sovrant.Integration.Tests;

/// <summary>
/// Live integration tests against a real OpenAI-compatible LLM provider.
/// Skipped by default — set <c>SOVRANT_LIVE_TESTS=1</c> plus <c>LLM_BASE_URL</c> and
/// <c>LLM_API_KEY</c> to run them. Provider-agnostic: works against OpenAI, OpenRouter,
/// Azure OpenAI, Together, Groq, Ollama, or any other OpenAI-compatible endpoint.
///
/// Run example (PowerShell):
/// <code>
///   $env:SOVRANT_LIVE_TESTS = "1"
///   $env:LLM_BASE_URL       = "https://openrouter.ai/api/v1"
///   $env:LLM_API_KEY        = "sk-or-v1-..."
///   $env:SOVRANT_LIVE_MODEL = "openai/gpt-oss-120b:free"
///   dotnet test tests/Sovrant.Integration.Tests
/// </code>
/// </summary>
public sealed class LiveProviderTests
{
    private static OpenAiCompatProvider CreateProvider()
    {
        var baseUrl = Environment.GetEnvironmentVariable("LLM_BASE_URL")
            ?? throw new InvalidOperationException("LLM_BASE_URL is not set.");
        var apiKey = Environment.GetEnvironmentVariable("LLM_API_KEY")
            ?? throw new InvalidOperationException("LLM_API_KEY is not set.");

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        var auth = new ApiKeyAuthProvider(apiKey);
        return new OpenAiCompatProvider(http, auth, NullLogger<OpenAiCompatProvider>.Instance);
    }

    private static string Model =>
        Environment.GetEnvironmentVariable("SOVRANT_LIVE_MODEL")
        ?? Environment.GetEnvironmentVariable("SOVRANT_MODEL")
        ?? "openai/gpt-oss-120b:free";

    [LiveFact]
    public async Task SendAsync_AgainstLiveProvider_ReturnsAssistantText()
    {
        var provider = CreateProvider();
        var req = new MessagesRequest(
            Model: Model,
            MaxTokens: 64,
            Messages: [InputMessage.UserText("Reply with exactly: pong")]);

        var result = await provider.SendAsync(req);

        Assert.True(result.IsSuccess, $"Provider call failed: {result.Error?.Message}");
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value!.Content);
    }

    [LiveFact]
    public async Task StreamAsync_AgainstLiveProvider_YieldsLifecycleEventsAndUsage()
    {
        var provider = CreateProvider();
        var req = new MessagesRequest(
            Model: Model,
            MaxTokens: 32,
            Messages: [InputMessage.UserText("Count from 1 to 3, one number per line.")])
        {
            Stream = true,
        };

        var sawMessageStart = false;
        var sawTextDelta = false;
        var sawMessageStop = false;
        var outputTokens = 0;

        await foreach (var ev in provider.StreamAsync(req))
        {
            switch (ev)
            {
                case StreamEvent.MessageStart:
                    sawMessageStart = true;
                    break;
                case StreamEvent.ContentBlockDelta { Delta: ContentBlockDelta.TextDelta }:
                    sawTextDelta = true;
                    break;
                case StreamEvent.MessageDelta md:
                    outputTokens = md.Usage.OutputTokens;
                    break;
                case StreamEvent.MessageStop:
                    sawMessageStop = true;
                    break;
            }
        }

        Assert.True(sawMessageStart, "Did not receive MessageStart");
        Assert.True(sawTextDelta, "Did not receive any TextDelta");
        Assert.True(sawMessageStop, "Did not receive MessageStop");
        Assert.True(outputTokens > 0,
            $"Trailing usage chunk did not report output tokens (got {outputTokens})");
    }

    [LiveFact]
    public async Task SendAsync_WithBogusModel_ReturnsFailureResult()
    {
        var provider = CreateProvider();
        var req = new MessagesRequest(
            Model: "definitely/not-a-real-model-zzz",
            MaxTokens: 16,
            Messages: [InputMessage.UserText("hi")]);

        var result = await provider.SendAsync(req);

        Assert.False(result.IsSuccess, "Bogus model should not return a successful result");
        Assert.NotNull(result.Error);
    }
}

/// <summary>
/// Marks a test as a live-provider integration test. Skipped at discovery time
/// unless the <c>SOVRANT_LIVE_TESTS</c> environment variable is set to <c>"1"</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("SOVRANT_LIVE_TESTS") != "1")
        {
            Skip = "Set SOVRANT_LIVE_TESTS=1 (with LLM_BASE_URL and LLM_API_KEY) to enable live provider tests.";
        }
    }
}
