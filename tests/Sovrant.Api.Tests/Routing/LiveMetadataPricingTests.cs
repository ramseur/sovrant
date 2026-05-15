using System.Text.Json;
using Sovrant.Api.Capabilities;

namespace Sovrant.Api.Tests.Routing;

/// <summary>Tests for Phase 48 enhancements to <see cref="LiveModelMetadataFetcher"/>.</summary>
public sealed class LiveMetadataPricingTests
{
    [Fact]
    public void ParseModelEntry_ExtractsPricing()
    {
        var json = """
        {
          "id": "openai/gpt-4o",
          "context_length": 128000,
          "supported_parameters": ["tools", "response_format"],
          "pricing": {
            "prompt": "0.0000025",
            "completion": "0.00001"
          },
          "top_provider": {
            "max_completion_tokens": 16384
          }
        }
        """;

        var doc = JsonDocument.Parse(json);
        var caps = LiveModelMetadataFetcher.ParseModelEntry(doc.RootElement);

        Assert.Equal(2.5m, caps.CostPerMillionInput);
        Assert.Equal(10m, caps.CostPerMillionOutput);
        Assert.Equal(128000, caps.MaxContext);
        Assert.Equal(16384, caps.MaxOutputTokens);
        Assert.True(caps.NativeTools);
        Assert.True(caps.StructuredOutput);
    }

    [Fact]
    public void ParseModelEntry_ExtractsReasoningSupport()
    {
        var json = """
        {
          "id": "anthropic/claude-opus-4-6",
          "context_length": 200000,
          "supported_parameters": ["tools", "response_format", "reasoning"],
          "pricing": {
            "prompt": "0.000015",
            "completion": "0.000075"
          }
        }
        """;

        var doc = JsonDocument.Parse(json);
        var caps = LiveModelMetadataFetcher.ParseModelEntry(doc.RootElement);

        Assert.True(caps.ThinkingMode);
        Assert.Equal(15m, caps.CostPerMillionInput);
        Assert.Equal(75m, caps.CostPerMillionOutput);
    }

    [Fact]
    public void ParseModelEntry_HandlesMissingPricing()
    {
        var json = """
        {
          "id": "local/llama-3",
          "context_length": 8192,
          "supported_parameters": []
        }
        """;

        var doc = JsonDocument.Parse(json);
        var caps = LiveModelMetadataFetcher.ParseModelEntry(doc.RootElement);

        Assert.Null(caps.CostPerMillionInput);
        Assert.Null(caps.CostPerMillionOutput);
        Assert.Null(caps.MaxOutputTokens);
    }

    [Fact]
    public void ParseModelEntry_HandlesZeroPricing()
    {
        var json = """
        {
          "id": "free/model",
          "context_length": 4096,
          "supported_parameters": [],
          "pricing": {
            "prompt": "0",
            "completion": "0"
          }
        }
        """;

        var doc = JsonDocument.Parse(json);
        var caps = LiveModelMetadataFetcher.ParseModelEntry(doc.RootElement);

        // Zero cost → null (not meaningful for tier assignment)
        Assert.Null(caps.CostPerMillionInput);
        Assert.Null(caps.CostPerMillionOutput);
    }

    [Fact]
    public void ParseModelEntry_HandlesNumericPricing()
    {
        // Some OpenRouter entries may return pricing as a number instead of string
        var json = """
        {
          "id": "test/model",
          "context_length": 4096,
          "supported_parameters": [],
          "pricing": {
            "prompt": 0.000005,
            "completion": 0.00002
          }
        }
        """;

        var doc = JsonDocument.Parse(json);
        var caps = LiveModelMetadataFetcher.ParseModelEntry(doc.RootElement);

        Assert.Equal(5m, caps.CostPerMillionInput);
        Assert.Equal(20m, caps.CostPerMillionOutput);
    }
}
