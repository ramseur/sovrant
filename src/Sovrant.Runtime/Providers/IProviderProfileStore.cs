namespace Sovrant.Runtime.Providers;

/// <summary>
/// CRUD over <c>provider_profiles</c>. Replaces direct read/write of
/// <c>~/.sovrant/providers.json</c>. Every API key referenced from a profile
/// flows through <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/>; this
/// store never sees plaintext secrets.
/// </summary>
public interface IProviderProfileStore
{
    /// <summary>Returns all profiles owned by <paramref name="userId"/>, ordered by created-at ascending.</summary>
    Task<IReadOnlyList<ProviderProfile>> ListAsync(string userId, CancellationToken ct = default);

    /// <summary>Returns the profile with the given id, or null if not found.</summary>
    Task<ProviderProfile?> GetAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Inserts <paramref name="profile"/>. Throws if a row with the same
    /// <see cref="ProviderProfile.ProfileId"/> already exists.
    /// </summary>
    Task CreateAsync(ProviderProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Replaces every mutable field on the row with the given id. Returns
    /// false if the id was not found.
    /// </summary>
    Task<bool> UpdateAsync(ProviderProfile profile, CancellationToken ct = default);

    /// <summary>Deletes the row. Returns false if the id was not found.</summary>
    Task<bool> DeleteAsync(string profileId, CancellationToken ct = default);
}
