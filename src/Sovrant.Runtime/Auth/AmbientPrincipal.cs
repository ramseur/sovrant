namespace Sovrant.Runtime.Auth;

/// <summary>
/// The user that work on the current async flow belongs to. A multi-user host sets it once per
/// authenticated request (Sovrant.Server's bearer-token middleware) or per background job (the
/// workflow scheduler, for the workflow's owner). Because it's an <see cref="AsyncLocal{T}"/>, it
/// flows into work started from that request even after the request ends, unlike
/// <c>IHttpContextAccessor</c>, which is cleared when the response completes.
/// </summary>
public static class AmbientPrincipal
{
    private static readonly AsyncLocal<Snapshot?> CurrentValue = new();

    /// <summary>The current flow's principal, or <c>null</c> when none was set.</summary>
    public static IPrincipalAccessor? Current => CurrentValue.Value;

    /// <summary>An <see cref="IPrincipalAccessor"/> that always reads the current flow (unauthenticated when none).</summary>
    public static IPrincipalAccessor Accessor { get; } = new CurrentAccessor();

    /// <summary>Sets the principal for the rest of this flow; disposing restores the previous one.</summary>
    public static IDisposable Push(string userId, string? role, string? workspaceId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        var previous = CurrentValue.Value;
        CurrentValue.Value = new Snapshot(userId, role, workspaceId);
        return new Restore(previous);
    }

    private sealed record Snapshot(string? UserId, string? Role, string? WorkspaceId) : IPrincipalAccessor
    {
        public bool IsAdmin => string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CurrentAccessor : IPrincipalAccessor
    {
        public string? UserId => CurrentValue.Value?.UserId;
        public string? Role => CurrentValue.Value?.Role;
        public bool IsAdmin => CurrentValue.Value?.IsAdmin ?? false;
        public string? WorkspaceId => CurrentValue.Value?.WorkspaceId;
    }

    private sealed class Restore(Snapshot? previous) : IDisposable
    {
        public void Dispose() => CurrentValue.Value = previous;
    }
}
