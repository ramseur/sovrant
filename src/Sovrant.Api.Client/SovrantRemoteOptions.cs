namespace Sovrant.Client.Remote;

/// <summary>Configuration for connecting to a remote Sovrant server.</summary>
public sealed class SovrantRemoteOptions
{
    /// <summary>Base URL of the Sovrant server (e.g. <c>http://localhost:5200</c>).</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Configuration DTO")]
    public string? Url { get; set; }

    /// <summary>Bearer token for authenticating with the server.</summary>
    public string? ApiToken { get; set; }

    /// <summary>SignalR-specific settings.</summary>
    public SignalROptions SignalR { get; set; } = new();
}

/// <summary>SignalR connection settings for remote mode.</summary>
public sealed class SignalROptions
{
    /// <summary>Whether to use SignalR for streaming (vs. SSE fallback). Default: true.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Reconnect interval in milliseconds. Default: 5000.</summary>
    public int ReconnectIntervalMs { get; set; } = 5000;

    /// <summary>Maximum reconnect attempts before giving up. Default: 10.</summary>
    public int MaxReconnectAttempts { get; set; } = 10;
}
