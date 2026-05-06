using Sovrant.Runtime.Auth;

namespace Sovrant.Server.Auth;

/// <summary>
/// Server implementation of <see cref="IPrincipalAccessor"/> — reads the
/// identity that <see cref="BearerTokenMiddleware"/> stamped onto
/// <see cref="IHttpContextAccessor.HttpContext"/>.
/// </summary>
internal sealed class HttpContextPrincipalAccessor : IPrincipalAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.Items[SovrantHttpContextKeys.UserId] as string;

    public string? Role =>
        _httpContextAccessor.HttpContext?.Items[SovrantHttpContextKeys.Role] as string;

    public bool IsAdmin =>
        string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);
}
