using System.Net.Http.Headers;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// Injects <c>Authorization: Bearer {token}</c> on every outgoing HTTP request
/// to the Sovrant server.
/// </summary>
public sealed class SovrantApiDelegatingHandler : DelegatingHandler
{
    private readonly SovrantRemoteOptions _options;
    private readonly RemoteConnectionState _connectionState;

    public SovrantApiDelegatingHandler(SovrantRemoteOptions options, RemoteConnectionState connectionState)
    {
        _options = options;
        _connectionState = connectionState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_options.ApiToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            _connectionState.Status = ConnectionStatus.Disconnected;

        return response;
    }
}
