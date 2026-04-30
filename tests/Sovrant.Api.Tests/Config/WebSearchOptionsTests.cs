using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Config;

namespace Sovrant.Api.Tests.Config;

public sealed class WebSearchOptionsTests : IDisposable
{
    private readonly string? _origSovrantWebSearch;
    private readonly string? _origLegacy;

    public WebSearchOptionsTests()
    {
        _origSovrantWebSearch = Environment.GetEnvironmentVariable("SOVRANT_WEB_SEARCH");
        _origLegacy = Environment.GetEnvironmentVariable("LLM_WEB_SEARCH");
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", null);
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", _origSovrantWebSearch);
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", _origLegacy);
    }

    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private static IConfiguration ConfigWith(string key, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

    [Fact]
    public void Resolve_NoSourceConfigured_ReturnsAuto()
    {
        var opts = WebSearchOptions.Resolve(EmptyConfig());

        Assert.Equal(WebSearchBackend.Auto, opts.Backend);
        Assert.False(opts.ResolvedFromLegacyEnv);
    }

    [Theory]
    [InlineData("auto", WebSearchBackend.Auto)]
    [InlineData("brave", WebSearchBackend.Brave)]
    [InlineData("Brave", WebSearchBackend.Brave)]
    [InlineData("BRAVE", WebSearchBackend.Brave)]
    [InlineData("firecrawl", WebSearchBackend.Firecrawl)]
    [InlineData("native", WebSearchBackend.Native)]
    [InlineData("off", WebSearchBackend.Off)]
    [InlineData("searxng-future", WebSearchBackend.SearxngFuture)]
    [InlineData("searxngfuture", WebSearchBackend.SearxngFuture)]
    public void Resolve_EnvVar_ParsesAllBackends(string envValue, WebSearchBackend expected)
    {
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", envValue);

        var opts = WebSearchOptions.Resolve(EmptyConfig());

        Assert.Equal(expected, opts.Backend);
    }

    [Fact]
    public void Resolve_EnvVar_BeatsConfig()
    {
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", "brave");

        var opts = WebSearchOptions.Resolve(ConfigWith("WebSearch:Backend", "firecrawl"));

        Assert.Equal(WebSearchBackend.Brave, opts.Backend);
    }

    [Fact]
    public void Resolve_ConfigKey_UsedWhenEnvAbsent()
    {
        var opts = WebSearchOptions.Resolve(ConfigWith("WebSearch:Backend", "native"));

        Assert.Equal(WebSearchBackend.Native, opts.Backend);
        Assert.False(opts.ResolvedFromLegacyEnv);
    }

    [Fact]
    public void Resolve_LegacyEnvVar_MapsToNative()
    {
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", "true");

        var opts = WebSearchOptions.Resolve(EmptyConfig());

        Assert.Equal(WebSearchBackend.Native, opts.Backend);
        Assert.True(opts.ResolvedFromLegacyEnv);
    }

    [Fact]
    public void Resolve_LegacyEnvVar_LosesToExplicitEnvVar()
    {
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", "true");
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", "off");

        var opts = WebSearchOptions.Resolve(EmptyConfig());

        Assert.Equal(WebSearchBackend.Off, opts.Backend);
        Assert.False(opts.ResolvedFromLegacyEnv);
    }

    [Fact]
    public void Resolve_LegacyEnvVar_LosesToConfigKey()
    {
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", "true");

        var opts = WebSearchOptions.Resolve(ConfigWith("WebSearch:Backend", "auto"));

        Assert.Equal(WebSearchBackend.Auto, opts.Backend);
        Assert.False(opts.ResolvedFromLegacyEnv);
    }

    [Fact]
    public void Resolve_LegacyEnvVar_EmitsDeprecationWarning()
    {
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", "true");
        var logger = new RecordingLogger();

        var opts = WebSearchOptions.Resolve(EmptyConfig(), logger);

        Assert.True(opts.ResolvedFromLegacyEnv);
        Assert.Single(logger.Warnings);
        Assert.Contains("LLM_WEB_SEARCH=true is deprecated", logger.Warnings[0]);
    }

    [Fact]
    public void Resolve_LegacyEnvVar_NotTrue_DoesNothing()
    {
        Environment.SetEnvironmentVariable("LLM_WEB_SEARCH", "false");

        var opts = WebSearchOptions.Resolve(EmptyConfig());

        Assert.Equal(WebSearchBackend.Auto, opts.Backend);
        Assert.False(opts.ResolvedFromLegacyEnv);
    }

    [Fact]
    public void Resolve_UnrecognizedEnvValue_FallsThrough()
    {
        Environment.SetEnvironmentVariable("SOVRANT_WEB_SEARCH", "duckduckgo");

        var opts = WebSearchOptions.Resolve(EmptyConfig());

        // Unrecognized → falls through to next source (config absent → default Auto).
        Assert.Equal(WebSearchBackend.Auto, opts.Backend);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}
