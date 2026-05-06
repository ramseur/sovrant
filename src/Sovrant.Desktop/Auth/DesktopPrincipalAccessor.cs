using Sovrant.Runtime.Auth;

namespace Sovrant.Desktop.Auth;

/// <summary>
/// Desktop implementation of <see cref="IPrincipalAccessor"/>.
/// Populated after login and held for the lifetime of the process.
/// </summary>
public sealed class DesktopPrincipalAccessor : IPrincipalAccessor
{
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);
}
