using Sovrant.Api.Auth;

namespace Sovrant.Desktop.Adapters;

/// <summary>
/// A mutable <see cref="IAuthProvider"/> that allows hot-swapping the API key
/// at runtime (e.g. after the setup wizard saves a new provider configuration).
/// </summary>
public sealed class MutableAuthProvider : IAuthProvider, IBaseUrlOverride
{
    private volatile string _apiKey;
    private volatile Uri? _baseUrl;

    public MutableAuthProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }

    /// <summary>
    /// When set, overrides the HttpClient's BaseAddress for all provider requests.
    /// Enables hot-swapping providers at runtime without recreating the HttpClient.
    /// </summary>
    public Uri? BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) =>
        ValueTask.FromResult(_apiKey);
}
