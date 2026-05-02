using System.Diagnostics.CodeAnalysis;

namespace Sovrant.Runtime.Providers;

/// <summary>
/// One saved provider configuration. Replaces the per-entry shape of
/// <c>~/.sovrant/providers.json</c>. The API key is stored separately in
/// <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/> under the
/// <see cref="CredentialId"/> key — this record holds only the reference.
/// </summary>
[SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite; matches existing CredentialConfig.BaseUrl convention.")]
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "BaseUrl persisted as TEXT in SQLite; matches existing CredentialConfig.BaseUrl convention.")]
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
    DateTimeOffset UpdatedAt);
