namespace Sovrant.Runtime.Preferences;

/// <summary>
/// Per-user key/value store for surface-facing settings (model, base URL,
/// max tokens, permission mode, intent routing, web search, active provider
/// profile id, …) that previously lived in <c>~/.sovrant/settings.json</c>.
/// API keys do <em>not</em> belong here — they go through
/// <see cref="Sovrant.Runtime.Mcp.ICredentialStore"/>; this table only ever
/// stores non-secret references like <c>provider.active_profile_id</c>.
/// </summary>
public interface IUserPreferenceStore
{
    /// <summary>Returns the value for <paramref name="key"/> for <paramref name="userId"/>, or null.</summary>
    Task<string?> GetAsync(string userId, string key, CancellationToken ct = default);

    /// <summary>Upserts a per-user value.</summary>
    Task SetAsync(string userId, string key, string value, CancellationToken ct = default);

    /// <summary>Deletes a per-user key (no-op if missing).</summary>
    Task DeleteAsync(string userId, string key, CancellationToken ct = default);

    /// <summary>Returns every preference for a user as a snapshot dictionary.</summary>
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// Well-known keys used in <see cref="IUserPreferenceStore"/>. Mirrors the
/// fields previously found in <c>SovrantConfig</c> JSON. Values are stored
/// as strings — bool/int/enum values use their <see cref="object.ToString"/>
/// invariant form.
/// </summary>
public static class UserPreferenceKeys
{
    /// <summary>Default LLM model id (e.g. <c>gpt-5</c>, <c>claude-opus-4-7</c>).</summary>
    public const string Model = "llm.model";

    /// <summary>Provider kind for the active profile (<c>OpenAI</c>, <c>OpenRouter</c>, …).</summary>
    public const string Provider = "llm.provider";

    /// <summary>Base URL of the active provider profile.</summary>
    public const string BaseUrl = "llm.base_url";

    /// <summary>Max tokens per response (integer, invariant culture).</summary>
    public const string MaxTokens = "llm.max_tokens";

    /// <summary>Permission mode (<c>Default</c>, <c>AcceptEdits</c>, <c>BypassPermissions</c>, <c>Plan</c>).</summary>
    public const string PermissionMode = "permissions.mode";

    /// <summary>Whether semantic intent routing is enabled (<c>true</c> / <c>false</c>).</summary>
    public const string IntentRouting = "intent.routing_enabled";

    /// <summary>Web search policy (<c>Off</c>, <c>Auto</c>, <c>Always</c>).</summary>
    public const string WebSearch = "web.search_mode";

    /// <summary>Active provider-profile id — foreign key into <c>provider_profiles</c>.</summary>
    public const string ActiveProviderProfileId = "provider.active_profile_id";

    /// <summary>
    /// Comma-separated list of MCP server names the user has opted into.
    /// Empty string or missing key means no MCP tools are exposed to agents (opt-in, not opt-out).
    /// </summary>
    public const string ActiveMcpServers = "mcp.active_servers";
}
