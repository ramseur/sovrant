using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Capabilities;

namespace Sovrant.Api.Tests.Capabilities;

/// <summary>Tests for <see cref="LiveModelMetadataFetcher"/>.</summary>
public sealed class LiveModelMetadataFetcherTests
{
    private readonly ModelCapabilityRegistry _registry = new(NullLogger<ModelCapabilityRegistry>.Instance);

    [Fact]
    public async Task FetchAsync_SkipsWhenNoOpenRouterKey()
    {
        var original = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);

            var http = new HttpClient(new FakeHttpMessageHandler(
                FakeHttpMessageHandler.JsonOk("{\"data\":[]}")));
            var fetcher = new LiveModelMetadataFetcher(_registry, http, NullLogger<LiveModelMetadataFetcher>.Instance);

            await fetcher.FetchAsync();

            Assert.Empty(_registry.GetAll());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", original);
        }
    }

    [Fact]
    public async Task FetchAsync_RegistersModelsFromResponse()
    {
        // Phase 88-C: keys are passed via CredentialConfig (or resolver),
        // not read from process environment. Pass the test key explicitly.
        var json = """
        {
          "data": [
            {
              "id": "google/gemma-4-27b",
              "context_length": 131072,
              "supported_parameters": ["tools", "tool_choice", "response_format"],
              "architecture": { "modality": "text" }
            },
            {
              "id": "meta/llama-3-70b",
              "context_length": 8192,
              "supported_parameters": ["temperature"]
            }
          ]
        }
        """;

        var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(json)));
        var credentials = new Sovrant.Api.Config.CredentialConfig { OpenRouterApiKey = "test-key" };
        var fetcher = new LiveModelMetadataFetcher(
            _registry, http, NullLogger<LiveModelMetadataFetcher>.Instance, credentials);

        await fetcher.FetchAsync();

        var all = _registry.GetAll();
        Assert.Equal(2, all.Count);

        var gemma = _registry.GetCapabilities("google/gemma-4-27b");
        Assert.True(gemma.NativeTools);
        Assert.True(gemma.StructuredOutput);
        Assert.Equal(131072, gemma.MaxContext);
        Assert.Equal(CapabilitySource.Live, gemma.Source);

        var llama = _registry.GetCapabilities("meta/llama-3-70b");
        Assert.False(llama.NativeTools);
        Assert.False(llama.StructuredOutput);
        Assert.Equal(8192, llama.MaxContext);
    }

    [Fact]
    public async Task FetchAsync_HandlesHttpFailureGracefully()
    {
        var original = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-key");

            var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.Unauthorized()));
            var fetcher = new LiveModelMetadataFetcher(_registry, http, NullLogger<LiveModelMetadataFetcher>.Instance);

            // Should not throw
            await fetcher.FetchAsync();

            Assert.Empty(_registry.GetAll());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", original);
        }
    }

    [Fact]
    public async Task FetchAsync_BundledOverridesTakePriority()
    {
        var original = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-key");

            // Register a bundled override first (higher priority)
            _registry.Register("google/gemma-4-27b", new ModelCapabilities
            {
                NativeTools = true,
                ThinkingMode = true,
                Source = CapabilitySource.Bundled,
            });

            var json = """
            {
              "data": [
                {
                  "id": "google/gemma-4-27b",
                  "context_length": 131072,
                  "supported_parameters": []
                }
              ]
            }
            """;

            var http = new HttpClient(new FakeHttpMessageHandler(FakeHttpMessageHandler.JsonOk(json)));
            var fetcher = new LiveModelMetadataFetcher(_registry, http, NullLogger<LiveModelMetadataFetcher>.Instance);

            await fetcher.FetchAsync();

            // Bundled (source=2) should NOT be overwritten by Live (source=1)
            var caps = _registry.GetCapabilities("google/gemma-4-27b");
            Assert.Equal(CapabilitySource.Bundled, caps.Source);
            Assert.True(caps.ThinkingMode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", original);
        }
    }
}
