using Sovrant.Api.Auth;

namespace Sovrant.Desktop.Adapters;

/// <summary>
/// A mutable <see cref="IAuthProvider"/> that allows hot-swapping the API key
/// at runtime (e.g. after the setup wizard saves a new provider configuration).
/// </summary>
public sealed class MutableAuthProvider : IAuthProvider
{
    private volatile string _apiKey;

    public MutableAuthProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    public string ApiKey
    {
        get => _apiKey;
        set => _apiKey = value;
    }

    public ValueTask<string> GetAuthHeaderAsync(CancellationToken ct) =>
        ValueTask.FromResult(_apiKey);
}
