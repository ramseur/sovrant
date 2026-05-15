using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Capabilities;
using Sovrant.Api.Config;
using Sovrant.Api.Providers;

namespace Sovrant.Api.Routing;

/// <summary>Default <see cref="IScopedProviderFactory"/> that creates an <see cref="OpenAiCompatProvider"/>.</summary>
public sealed class DefaultScopedProviderFactory : IScopedProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly WebSearchOptions? _webSearch;
    private readonly IModelCapabilityRegistry? _capabilities;
    private readonly CredentialConfig? _credentials;

    public DefaultScopedProviderFactory(ILoggerFactory loggerFactory)
        : this(loggerFactory, webSearch: null, capabilities: null, credentials: null) { }

    /// <summary>
    /// Constructs the factory with the dependencies required for the
    /// centralised <see cref="Sovrant.Api.OpenAi.NativeWebSearchInjector"/>.
    /// All optional arguments may be <see langword="null"/> — when so the
    /// produced provider falls back to the legacy code path.
    /// </summary>
    public DefaultScopedProviderFactory(
        ILoggerFactory loggerFactory,
        WebSearchOptions? webSearch,
        IModelCapabilityRegistry? capabilities,
        CredentialConfig? credentials)
    {
        _loggerFactory = loggerFactory;
        _webSearch = webSearch;
        _capabilities = capabilities;
        _credentials = credentials;
    }

    /// <inheritdoc/>
    public ISmartRouter Create(HttpClient http, string apiKey)
    {
        var auth = new ApiKeyAuthProvider(apiKey);
        var logger = _loggerFactory.CreateLogger("Sovrant.Api.ScopedProvider");
        var provider = new OpenAiCompatProvider(http, auth, logger, _webSearch, _capabilities, _credentials);
        return new ScopedSingleProviderRouter(provider);
    }
}
