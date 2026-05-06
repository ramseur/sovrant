namespace Sovrant.Runtime.Auth;

/// <summary>
/// Provides the identity of the currently authenticated caller without
/// coupling business logic to HTTP, Blazor session, or desktop session concerns.
/// </summary>
public interface IPrincipalAccessor
{
    /// <summary>The authenticated user's ID, or <c>null</c> if not authenticated.</summary>
    string? UserId { get; }

    /// <summary>The authenticated user's role (<c>"admin"</c> or <c>"user"</c>), or <c>null</c>.</summary>
    string? Role { get; }

    /// <summary>True when the caller is authenticated and has the <c>"admin"</c> role.</summary>
    bool IsAdmin { get; }
}
