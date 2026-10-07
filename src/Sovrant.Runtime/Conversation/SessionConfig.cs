using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Per-session configuration overlay that shadows global server defaults.
/// Each pooled session carries its own <see cref="SessionConfig"/> so that
/// model and permission mode changes are scoped to a single session.
/// </summary>
public sealed class SessionConfig
{
    /// <summary>The conversation this config belongs to (set by the session pool). Phase 145: approvals are routed by it.</summary>
    public string? SessionId { get; set; }

    /// <summary>Who owns the conversation (set by the session pool), or null for single-user hosts.</summary>
    public string? OwnerUserId { get; set; }

    /// <summary>
    /// Phase 145: the shared, admin-configured provider profile this conversation uses (the member's
    /// pick), or null for the install default. <c>SessionProviderRouter</c> routes by it.
    /// </summary>
    public string? ProviderProfileId { get; set; }

    private volatile string? _model;
    private volatile int _permissionModeInt = -1; // -1 = use global default
    private IReadOnlyList<string>? _allowedMcpServers;
    private HashSet<string>? _allowedMcpServersSet;

    /// <summary>
    /// Names of MCP servers whose tools are exposed to the LLM for this session.
    /// <see langword="null"/> means no gating — every connected server's tools are available.
    /// An empty list disables all MCP tools.
    /// </summary>
    public IReadOnlyList<string>? AllowedMcpServers
    {
        get => _allowedMcpServers;
        set
        {
            _allowedMcpServers = value;
            _allowedMcpServersSet = value is null
                ? null
                : new HashSet<string>(value, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Set-backed view of <see cref="AllowedMcpServers"/> for O(1) membership
    /// tests in hot paths. Rebuilt only when the list is reassigned.
    /// </summary>
    internal IReadOnlySet<string>? AllowedMcpServersSet => _allowedMcpServersSet;

    /// <summary>
    /// When set, the session runtime uses this system prompt instead of the default Sovrant prompt.
    /// Used for agent-scoped chat sessions launched from the Agents page.
    /// </summary>
    public string? SystemPromptOverride { get; set; }

    /// <summary>
    /// Name of the agent template this session is scoped to, if any.
    /// Informational — used for display in the sidebar and Command Center.
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Per-session model override. <see langword="null"/> means use the global default.
    /// </summary>
    public string? Model
    {
        get => _model;
        set => _model = value;
    }

    /// <summary>
    /// Per-session permission mode override. <see langword="null"/> means use the global default.
    /// </summary>
    public PermissionMode? PermissionMode
    {
        get
        {
            var v = _permissionModeInt;
            return v < 0 ? null : (PermissionMode)v;
        }
        set => _permissionModeInt = value.HasValue ? (int)value.Value : -1;
    }

    private long _totalInputTokens;
    private long _totalOutputTokens;

    /// <summary>Total input tokens consumed across all turns in this session.</summary>
    public long TotalInputTokens => Interlocked.Read(ref _totalInputTokens);

    /// <summary>Total output tokens generated across all turns in this session.</summary>
    public long TotalOutputTokens => Interlocked.Read(ref _totalOutputTokens);

    /// <summary>Atomically adds <paramref name="inputTokens"/> and <paramref name="outputTokens"/> to the running totals.</summary>
    public void AddTokens(int inputTokens, int outputTokens)
    {
        if (inputTokens > 0) Interlocked.Add(ref _totalInputTokens, inputTokens);
        if (outputTokens > 0) Interlocked.Add(ref _totalOutputTokens, outputTokens);
    }
}
