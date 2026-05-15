using Sovrant.Api.Auth;

namespace Sovrant.Web.Adapters;

/// <summary>
/// Mutable auth provider that allows hot-swapping API key and base URL at runtime
/// when the user changes providers in Settings.
/// </summary>
public sealed class MutableAuthProvider : IAuthProvider, IBaseUrlOverride
{
    private volatile string _apiKey;
    private volatile Uri? _baseUrl;

    public MutableAuthProvider(string apiKey, Uri? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl;
    }

    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }

    public Uri? BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) =>
        ValueTask.FromResult(_apiKey);
}
