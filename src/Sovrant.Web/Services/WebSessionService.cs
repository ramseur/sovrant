using Sovrant.Runtime.Auth;

namespace Sovrant.Web.Services;

/// <summary>
/// Singleton auth-state holder for the embedded Web instance.
/// Implements <see cref="IPrincipalAccessor"/> so DI consumers get the signed-in identity.
/// </summary>
public sealed class WebSessionService : IPrincipalAccessor
{
    private volatile string? _userId;
    private volatile string? _role;
    private volatile string? _workspaceId;

    public string? UserId => _userId;
    public string? Role => _role;
    public bool IsAdmin => string.Equals(_role, "admin", StringComparison.OrdinalIgnoreCase);
    public string? WorkspaceId => _workspaceId;
    public bool IsAuthenticated => _userId is not null;

    public void SignIn(string userId, string role)
    {
        _userId = userId;
        _role = role;
    }

    public void SignOut()
    {
        _userId = null;
        _role = null;
        _workspaceId = null;
    }

    public void SetWorkspace(string? workspaceId) => _workspaceId = workspaceId;
}
