using System.Collections.Concurrent;

namespace Sovrant.Runtime.Mcp;

/// <summary>Connection state of one configured MCP server (Phase 139).</summary>
public enum McpServerState
{
    Connecting,
    Connected,
    Unavailable,
}

/// <summary>
/// Status of one MCP server for the Integrations page and the top-bar menu. For an unavailable
/// server, <see cref="Message"/> is the friendly sentence, ending with what happens next.
/// </summary>
public sealed record McpServerStatus(
    string Name,
    McpServerState State,
    McpConnectionFailure? Failure = null,
    DateTimeOffset? NextRetryAt = null,
    int Attempt = 0,
    int MaxAttempts = 0)
{
    public bool IsUnavailable => State == McpServerState.Unavailable;

    /// <summary>"Couldn't reach api.pixellab.ai. Check your internet connection. Retrying automatically."</summary>
    public string Message
    {
        get
        {
            if (Failure is null)
                return State == McpServerState.Connecting ? "Connecting…" : string.Empty;
            var next = NextRetryAt is not null
                ? "Retrying automatically."
                : Failure.ShouldRetry ? "Automatic retries stopped; use Retry in Integrations." : null;
            return string.Join(' ', new[] { Failure.Reason + ".", Failure.Advice, next }.Where(s => !string.IsNullOrEmpty(s)));
        }
    }

    /// <summary>Everything after the reason: the advice and what happens next (the alert's second line).</summary>
    public string NextStep => Failure is null ? string.Empty : Message[Math.Min(Message.Length, Failure.Reason.Length + 1)..].Trim();

    /// <summary>"Next attempt in 52 s · retry 2 of 3", or the credential note; empty when not unavailable.</summary>
    public string RetryNote(DateTimeOffset now)
    {
        if (Failure is null) return string.Empty;
        if (!Failure.ShouldRetry) return "Not retried automatically: retrying can't fix a key.";
        if (NextRetryAt is not { } at) return $"All {MaxAttempts} automatic retries failed.";
        var wait = at - now;
        var waitText = wait.TotalSeconds < 1 ? "a moment"
            : wait.TotalMinutes < 1 ? $"{Math.Ceiling(wait.TotalSeconds):0} s"
            : $"{Math.Ceiling(wait.TotalMinutes):0} min";
        return $"Next attempt in {waitText} · retry {Attempt + 1} of {MaxAttempts}";
    }
}

/// <summary>Event data for <see cref="McpServerStatusRegistry.Changed"/>.</summary>
public sealed class McpServerStatusChangedEventArgs(string name) : EventArgs
{
    public string Name { get; } = name;
}

/// <summary>
/// Phase 139 — per-server MCP connection status, written by <see cref="McpToolRegistrar"/> and
/// read by the UIs. <see cref="Changed"/> fires (on a background thread) whenever a status changes.
/// </summary>
public sealed class McpServerStatusRegistry
{
    private readonly ConcurrentDictionary<string, McpServerStatus> _statuses = new(StringComparer.Ordinal);

    /// <summary>Raised with the server name after its status changes or is removed.</summary>
    public event EventHandler<McpServerStatusChangedEventArgs>? Changed;

    public IReadOnlyDictionary<string, McpServerStatus> All => _statuses;

    public McpServerStatus? Get(string name) => _statuses.TryGetValue(name, out var s) ? s : null;

    public bool IsUnavailable(string name) => Get(name)?.IsUnavailable == true;

    public void Set(McpServerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        _statuses[status.Name] = status;
        Changed?.Invoke(this, new McpServerStatusChangedEventArgs(status.Name));
    }

    public void Remove(string name)
    {
        if (_statuses.TryRemove(name, out _))
            Changed?.Invoke(this, new McpServerStatusChangedEventArgs(name));
    }
}
