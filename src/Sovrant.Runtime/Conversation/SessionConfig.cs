using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Per-session configuration overlay that shadows global server defaults.
/// Each pooled session carries its own <see cref="SessionConfig"/> so that
/// model and permission mode changes are scoped to a single session.
/// </summary>
public sealed class SessionConfig
{
    private volatile string? _model;
    private volatile int _permissionModeInt = -1; // -1 = use global default

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
