namespace Sovrant.Api.Auth;

/// <summary>Provides authorization headers for API requests.</summary>
public interface IAuthProvider
{
    /// <summary>Gets the value to use for the Authorization Bearer token.</summary>
    ValueTask<string> GetAuthHeaderAsync(CancellationToken ct);
}

/// <summary>Authenticates using a static API key as a Bearer token.</summary>
public sealed class ApiKeyAuthProvider(string apiKey) : IAuthProvider
{
    /// <inheritdoc/>
    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) => ValueTask.FromResult(apiKey);
}
