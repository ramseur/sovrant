using System.Diagnostics.CodeAnalysis;

namespace Sovrant.Runtime.Providers;

/// <summary>
/// One saved provider configuration. The API key is stored separately in
/// <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/> under <see cref="CredentialId"/> —
/// this record holds only the reference.
/// </summary>
/// <param name="WorkspaceId">
/// When non-null, this is a workspace-level profile configured by an admin.
/// Non-admin members can use it but cannot view or modify the credential.
/// Null means a personal profile owned by <see cref="UserId"/>.
/// </param>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite.")]
public sealed record ProviderProfile(
    string ProfileId,
    string UserId,
    string Name,
    string ProviderKind,
    string BaseUrl,
    string? DefaultModel,
    int? MaxTokens,
    string CredentialId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? WorkspaceId = null)

{
    /// <summary>True when this profile was set by a workspace admin and the key is not user-visible.</summary>
    public bool IsAdminManaged => WorkspaceId is not null;
}
