using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Providers;

/// <summary>
/// Phase 138 — keeps the running provider in step with the active workspace. Admins decide
/// which provider profiles each workspace may use (<c>provider.enabled_profile_ids</c>). When
/// the user's saved profile isn't available in the workspace they switch to, its endpoint is
/// switched off for the process (so, for example, an Ollama profile from another workspace is
/// never contacted). When it becomes available again, it's switched back on. Saved preferences
/// are never changed.
/// </summary>
public static class ActiveProviderGuard
{
    /// <summary>Shown when a turn starts while the saved profile isn't enabled for the workspace.</summary>
    public const string NotEnabledReason =
        "No provider is active for this workspace: your saved provider isn't enabled here. " +
        "Pick a model from the model menu, or ask an admin to enable a provider for this workspace.";

    /// <summary>The parts of a provider profile needed to (re)activate it.</summary>
    public sealed record AvailableProfile(string? CredentialId, Uri? BaseUrl, int MaxTokens)
    {
        /// <summary>Builds from a profile row's stored base URL string (blank or invalid → default endpoint).</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings",
            Justification = "Profile rows store the base URL as a string; this is the one place it's parsed.")]
        public static AvailableProfile From(string? credentialId, string? baseUrl, int maxTokens) =>
            new(credentialId,
                !string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed) ? parsed : null,
                maxTokens);
    }

    /// <summary>
    /// Reconciles the running provider with the profiles available in the current workspace.
    /// </summary>
    /// <param name="config">The live runtime config.</param>
    /// <param name="savedProfileId">The user's saved active profile id; nothing happens when empty.</param>
    /// <param name="available">That profile as listed for the current workspace, or <see langword="null"/> when it isn't available there.</param>
    /// <param name="savedModel">The user's saved model, re-applied on re-activation.</param>
    /// <param name="credentials">Credential store for the profile's API key.</param>
    /// <param name="applyAuth">Pushes the key and base URL into the surface's auth provider.</param>
    /// <returns><see langword="true"/> when the running provider was switched off or back on.</returns>
    public static async Task<bool> ReconcileAsync(
        SovrantConfig config,
        string? savedProfileId,
        AvailableProfile? available,
        string? savedModel,
        ICredentialStore credentials,
        Action<string, Uri?> applyAuth,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(applyAuth);

        if (string.IsNullOrEmpty(savedProfileId))
            return false;

        if (available is null)
        {
            if (config.InactiveProviderReason is not null)
                return false;
            config.DeactivateProvider(NotEnabledReason);
            applyAuth(string.Empty, null);
            return true;
        }

        if (config.InactiveProviderReason is null)
            return false;

        var rawKey = string.IsNullOrWhiteSpace(available.CredentialId)
            ? string.Empty
            : await credentials.RetrieveAsync(available.CredentialId, ct).ConfigureAwait(false) ?? string.Empty;
        var apiKey = new string(rawKey.Where(c => c < 128).ToArray()).Trim();
        var baseUrl = available.BaseUrl;

        config.ApiKey = apiKey;
        config.MaxTokens = available.MaxTokens;
        if (!string.IsNullOrWhiteSpace(savedModel))
            config.Model = savedModel;
        config.ActivateProvider(baseUrl);
        applyAuth(apiKey, baseUrl);
        return true;
    }
}
