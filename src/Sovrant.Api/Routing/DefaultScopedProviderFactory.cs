using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Api.Providers;

namespace Sovrant.Api.Routing;

/// <summary>Default <see cref="IScopedProviderFactory"/> that creates an <see cref="OpenAiCompatProvider"/>.</summary>
public sealed class DefaultScopedProviderFactory : IScopedProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public DefaultScopedProviderFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public ISmartRouter Create(HttpClient http, string apiKey)
    {
        var auth = new ApiKeyAuthProvider(apiKey);
        var logger = _loggerFactory.CreateLogger("Sovrant.Api.ScopedProvider");
        var provider = new OpenAiCompatProvider(http, auth, logger);
        return new ScopedSingleProviderRouter(provider);
    }
}
