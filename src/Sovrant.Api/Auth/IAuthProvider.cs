namespace Sovrant.Api.Auth;

/// <summary>Provides authorization headers for API requests.</summary>
public interface IAuthProvider
{
    /// <summary>Gets the value to use for the Authorization Bearer token.</summary>
    ValueTask<string> GetAuthHeaderAsync(CancellationToken ct);
}

/// <summary>
/// Optional interface for auth providers that can override the base URL at runtime.
/// Used by desktop apps to hot-swap providers without recreating the HttpClient.
/// </summary>
public interface IBaseUrlOverride
{
    /// <summary>When non-null, overrides the HttpClient's BaseAddress for requests.</summary>
    Uri? BaseUrl { get; }
}

/// <summary>Authenticates using a static API key as a Bearer token.</summary>
public sealed class ApiKeyAuthProvider(string apiKey) : IAuthProvider
{
    /// <inheritdoc/>
    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) => ValueTask.FromResult(apiKey);
}
