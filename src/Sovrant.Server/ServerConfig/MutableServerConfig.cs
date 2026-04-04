using Sovrant.Runtime.Permissions;

namespace Sovrant.Server.ServerConfig;

/// <summary>
/// Thread-safe mutable runtime configuration for the server.
/// Holds the live values for model, API key, permission mode, and provider pin —
/// all of which can be updated at runtime via <c>PUT /v1/config</c>.
/// </summary>
public sealed class MutableServerConfig
{
    private volatile string _model;
    private volatile string _llmApiKey;
    private volatile string _llmBaseUrl;
    private volatile string? _pinnedProvider;
    private volatile int _permissionModeInt; // PermissionMode enum stored as int for volatile

    public MutableServerConfig(string model, string llmApiKey, string llmBaseUrl, PermissionMode permissionMode)
    {
        _model = model;
        _llmApiKey = llmApiKey;
        _llmBaseUrl = llmBaseUrl;
        _permissionModeInt = (int)permissionMode;
    }

    /// <summary>The active LLM model identifier.</summary>
    public string Model
    {
        get => _model;
        set => _model = value;
    }

    /// <summary>The API key used to authenticate with the LLM provider.</summary>
    public string LlmApiKey
    {
        get => _llmApiKey;
        set => _llmApiKey = value;
    }

    /// <summary>The base URL of the LLM provider. Changing this requires a server restart.</summary>
    public string LlmBaseUrl
    {
        get => _llmBaseUrl;
        set => _llmBaseUrl = value;
    }

    /// <summary>The currently pinned provider name, or <see langword="null"/> for smart routing.</summary>
    public string? PinnedProvider
    {
        get => _pinnedProvider;
        set => _pinnedProvider = value;
    }

    /// <summary>The active permission mode.</summary>
    public PermissionMode PermissionMode
    {
        get => (PermissionMode)_permissionModeInt;
        set => _permissionModeInt = (int)value;
    }
}
