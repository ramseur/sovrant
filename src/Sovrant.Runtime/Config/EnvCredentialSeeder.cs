using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sovrant.Api.Auth;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Config;

/// <summary>
/// Phase 144 — provider API keys from the environment (shell, container env or <c>.env</c>), for
/// headless/containerised installs.
/// <list type="bullet">
///   <item><b>Boot:</b> each key's env value is imported into the encrypted credential store when
///   the store has no value yet. After that the store wins, so admin edits in the UI stick.
///   <c>SOVRANT_ENV_KEYS_OVERRIDE=true</c> re-imports env values on every start (CI, ephemeral containers).</item>
///   <item><b>Per user:</b> when <c>LLM_API_KEY</c> is set and a signed-in user has no provider of their
///   own yet, they get a provider profile for it (base URL from <c>LLM_BASE_URL</c> or inferred
///   from the key, model from <c>SOVRANT_MODEL</c>), enabled for their personal workspace and made
///   active, so it appears in the model menu with nothing to set up.</item>
/// </list>
/// Values are never logged; only variable names.
/// </summary>
public static partial class EnvCredentialSeeder
{
    public const string OverrideVariable = "SOVRANT_ENV_KEYS_OVERRIDE";

    /// <summary>The credential every env-created profile points at (one key, updated in one place).</summary>
    public const string EnvProfileCredentialId = "provider.env.api_key";

    /// <summary>Env variable(s) → credential-store key. The first non-empty variable wins.</summary>
    public static readonly IReadOnlyList<(string CredentialKey, string[] Variables)> Keys =
    [
        (CredentialKeys.LlmApiKey, ["LLM_API_KEY", "OPENAI_API_KEY"]),
        (CredentialKeys.ProviderApiKey, ["PROVIDER_API_KEY"]), // the native messages-API provider (PROVIDER_BASE_URL)
        (CredentialKeys.OpenRouterApiKey, ["OPENROUTER_API_KEY"]),
        (CredentialKeys.BraveApiKey, ["BRAVE_API_KEY"]),
        (CredentialKeys.FirecrawlApiKey, ["FIRECRAWL_API_KEY"]),
    ];

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Variable} from the environment into the encrypted credential store")]
    private static partial void LogImported(ILogger logger, string variable);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created provider profile '{ProfileId}' for {UserId} from LLM_API_KEY")]
    private static partial void LogProfileCreated(ILogger logger, string profileId, string userId);

    /// <summary>The LLM key from the environment, if any (LLM_API_KEY, then its aliases).</summary>
    public static string? LlmKey(Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return FirstValue(env, Keys[0].Variables);
    }

    /// <summary>True when SOVRANT_ENV_KEYS_OVERRIDE is "true" or "1".</summary>
    public static bool OverrideEnabled(Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(env);
        var value = env(OverrideVariable)?.Trim();
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    /// <summary>
    /// Imports env keys into the credential store (first boot, or every start with the override).
    /// Returns the variable names that were imported.
    /// </summary>
    public static async Task<IReadOnlyList<string>> SeedAsync(
        ICredentialStore store, Func<string, string?> env, ILogger? logger = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(env);
        var force = OverrideEnabled(env);
        var imported = new List<string>();

        foreach (var (credentialKey, variables) in Keys)
        {
            var variable = variables.FirstOrDefault(v => !string.IsNullOrWhiteSpace(env(v)));
            if (variable is null) continue;
            var value = env(variable)!.Trim();

            var existing = await store.RetrieveAsync(credentialKey, ct).ConfigureAwait(false);
            if (!force && !string.IsNullOrEmpty(existing)) continue;
            if (string.Equals(existing, value, StringComparison.Ordinal)) continue;

            await store.StoreAsync(credentialKey, value, ct).ConfigureAwait(false);
            imported.Add(variable);
            if (logger is not null) LogImported(logger, variable);
        }

        // The env-created profiles share one credential; keep it in step with the env key.
        if (LlmKey(env) is { } llmKey)
        {
            var existing = await store.RetrieveAsync(EnvProfileCredentialId, ct).ConfigureAwait(false);
            if ((force || string.IsNullOrEmpty(existing)) && !string.Equals(existing, llmKey, StringComparison.Ordinal))
                await store.StoreAsync(EnvProfileCredentialId, llmKey, ct).ConfigureAwait(false);
        }
        return imported;
    }

    /// <summary>
    /// Applies <c>LLM_BASE_URL</c> and <c>SOVRANT_MODEL</c> as the install-wide defaults when no
    /// saved preference sets them (users' own choices are applied afterwards and win).
    /// </summary>
    public static void ApplyDefaults(SovrantConfig config, Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(env);
        if (config.BaseUrl is null && LlmKey(env) is { } key)
            config.BaseUrl = BaseUrlFor(key, env);
        if (env("SOVRANT_MODEL") is { } model && !string.IsNullOrWhiteSpace(model) && PreferenceValidation.IsValidModelName(model.Trim()))
            config.Model = model.Trim();
    }

    /// <summary>
    /// Gives a user without any provider a profile for the env LLM key (see class summary).
    /// Does nothing when LLM_API_KEY isn't set, or the user already has a provider or an active choice.
    /// </summary>
    public static async Task EnsureUserProviderAsync(
        IServiceProvider services, string userId, Func<string, string?> env, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(env);
        if (string.IsNullOrEmpty(userId) || LlmKey(env) is not { } key)
            return;

        var profiles = services.GetService<IProviderProfileStore>();
        var prefs = services.GetService<IUserPreferenceStore>();
        var workspaces = services.GetService<IWorkspaceService>();
        var wsSettings = services.GetService<IWorkspaceSettingsStore>();
        if (profiles is null || prefs is null || workspaces is null || wsSettings is null)
            return;

        // Only for real, signed-up users: registration creates the personal workspace. Runtime start-up
        // also applies preferences for the OS account name, which may have no user row; skip it.
        var personal = await workspaces.GetPersonalAsync(userId, ct).ConfigureAwait(false);
        if (personal is null)
            return;
        if (!string.IsNullOrEmpty(await prefs.GetAsync(userId, UserPreferenceKeys.ActiveProviderProfileId, ct).ConfigureAwait(false)))
            return;
        if ((await profiles.ListAsync(userId, ct).ConfigureAwait(false)).Count > 0)
            return;

        var baseUrl = BaseUrlFor(key, env);
        var kind = KindFor(baseUrl);
        var model = env("SOVRANT_MODEL")?.Trim();
        if (string.IsNullOrEmpty(model) || !PreferenceValidation.IsValidModelName(model))
            model = null;

        var now = DateTimeOffset.UtcNow;
        var profileId = ProfileIdFor(userId);
        await profiles.CreateAsync(new ProviderProfile(
            ProfileId: profileId,
            UserId: userId,
            Name: $"{kind} (from environment)",
            ProviderKind: kind,
            BaseUrl: baseUrl.ToString(),
            DefaultModel: model,
            MaxTokens: 32000,
            CredentialId: EnvProfileCredentialId,
            CreatedAt: now,
            UpdatedAt: now), ct).ConfigureAwait(false);

        // Enable it for the user's personal workspace (workspace enablement is strict opt-in).
        var raw = await wsSettings.GetAsync(personal.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, ct).ConfigureAwait(false);
        var ids = (raw ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!ids.Contains(profileId, StringComparer.Ordinal))
        {
            ids.Add(profileId);
            await wsSettings.SetAsync(personal.WorkspaceId, WorkspaceSettingsKeys.EnabledProviderProfileIds, string.Join(',', ids), ct).ConfigureAwait(false);
        }

        await prefs.SetAsync(userId, UserPreferenceKeys.ActiveProviderProfileId, profileId, ct).ConfigureAwait(false);
        await prefs.SetAsync(userId, UserPreferenceKeys.Provider, kind, ct).ConfigureAwait(false);
        await prefs.SetAsync(userId, UserPreferenceKeys.BaseUrl, baseUrl.ToString(), ct).ConfigureAwait(false);
        if (model is not null)
            await prefs.SetAsync(userId, UserPreferenceKeys.Model, model, ct).ConfigureAwait(false);

        if (services.GetService<ILoggerFactory>()?.CreateLogger(typeof(EnvCredentialSeeder)) is { } log)
            LogProfileCreated(log, profileId, userId);
    }

    /// <summary>LLM_BASE_URL when set; otherwise inferred from the key's prefix (OpenRouter, Anthropic), else OpenAI.</summary>
    public static Uri BaseUrlFor(string key, Func<string, string?> env)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(env);
        if (env("LLM_BASE_URL") is { } raw && Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var url))
            return url;
        if (key.StartsWith("sk-or-", StringComparison.Ordinal))
            return new Uri("https://openrouter.ai/api/v1");
        if (key.StartsWith("sk-ant-", StringComparison.Ordinal))
            return new Uri("https://api.anthropic.com/v1");
        return new Uri("https://api.openai.com/v1");
    }

    /// <summary>A provider kind label for the base URL (matches the names the setup screens use).</summary>
    public static string KindFor(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        var host = baseUrl.Host;
        if (host.EndsWith("openrouter.ai", StringComparison.OrdinalIgnoreCase)) return "OpenRouter";
        if (host.EndsWith("anthropic.com", StringComparison.OrdinalIgnoreCase)) return "Anthropic";
        if (host.EndsWith("openai.com", StringComparison.OrdinalIgnoreCase)) return "OpenAI";
        if (baseUrl.Port == 11434) return "Ollama";
        if (baseUrl.Port == 1234) return "LM Studio";
        return "OpenAI-compatible";
    }

    private static string ProfileIdFor(string userId) =>
        "env-" + new string(userId.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray()).Trim('-');

    private static string? FirstValue(Func<string, string?> env, IEnumerable<string> variables) =>
        variables.Select(v => env(v)?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));
}
