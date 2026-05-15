using System.Net.Http.Headers;
using System.Text.Json;
using Sovrant.Runtime.Auth;

namespace Sovrant.Client.Remote;

/// <summary>
/// <see cref="IIdentityService"/> adapter that calls the Sovrant server's auth endpoints.
/// Used by Desktop and CLI in remote mode so the existing login/register UI works unchanged.
/// </summary>
public sealed class RemoteIdentityService : IIdentityService
{
    private readonly HttpClient _http;
    private readonly SovrantRemoteOptions _options;

    public RemoteIdentityService(IHttpClientFactory httpClientFactory, SovrantRemoteOptions options)
    {
        _http = httpClientFactory.CreateClient("SovrantApi");
        _options = options;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { email, password });
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(new Uri("/v1/auth/login", UriKind.Relative), content, ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var errMsg = TryExtractError(errBody) ?? $"Login failed ({(int)resp.StatusCode}).";
            return new LoginResult(false, null, null, null, errMsg);
        }

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
        var userId = doc.RootElement.TryGetProperty("user_id", out var u) ? u.GetString() : null;
        var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : "user";

        // Hot-swap the token into options so subsequent requests in this process are authenticated.
        if (!string.IsNullOrEmpty(token))
            _options.ApiToken = token;

        return new LoginResult(true, token, userId, role, null);
    }

    public async Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { email, password });
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(new Uri("/v1/auth/register", UriKind.Relative), content, ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var errMsg = TryExtractError(errBody) ?? $"Registration failed ({(int)resp.StatusCode}).";
            return new RegisterResult(false, null, null, null, errMsg);
        }

        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        var token = doc.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
        var userId = doc.RootElement.TryGetProperty("user_id", out var u) ? u.GetString() : null;
        var role = doc.RootElement.TryGetProperty("role", out var r) ? r.GetString() : null;
        var isPending = doc.RootElement.TryGetProperty("pending_approval", out var p) && p.GetBoolean();

        if (!string.IsNullOrEmpty(token))
            _options.ApiToken = token;

        return new RegisterResult(true, token, userId, role, null, isPending);
    }

    public async Task<bool> IsRegistrationOpenAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(new Uri("/v1/auth/registration/status", UriKind.Relative), ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) return false;
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("registration_open", out var o) && o.GetBoolean();
    }

    public async Task<bool> IsFirstRunAsync(CancellationToken ct = default)
    {
        // Server doesn't expose first-run state directly; treat open registration as first-run proxy.
        return await IsRegistrationOpenAsync(ct).ConfigureAwait(false);
    }

    public Task LogoutAsync(string tokenId, CancellationToken ct = default)
    {
        _options.ApiToken = null;
        return Task.CompletedTask;
    }

    // Admin-only methods not needed in remote client context.
    public Task<bool> IsApprovalRequiredAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task SetApprovalRequiredAsync(bool required, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> ApproveUserAsync(string userId, CancellationToken ct = default) => Task.FromResult(false);
    public Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default) => Task.CompletedTask;
    public Task<string> GenerateResetTokenAsync(string targetUserId, CancellationToken ct = default) => Task.FromResult(string.Empty);
    public Task<ResetPasswordResult> UseResetTokenAsync(string plaintextToken, string newPassword, CancellationToken ct = default) => Task.FromResult(new ResetPasswordResult(false, "Not available in remote mode."));
    public Task SetRegistrationOpenAsync(bool open, CancellationToken ct = default) => Task.CompletedTask;

    private static string? TryExtractError(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString();
            if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString();
        }
        catch { }
        return null;
    }
}
