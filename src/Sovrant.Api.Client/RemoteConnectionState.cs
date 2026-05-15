namespace Sovrant.Client.Remote;

/// <summary>Observable connection state for the remote Sovrant server.</summary>
public sealed class RemoteConnectionState
{
    private ConnectionStatus _status = ConnectionStatus.Disconnected;

    public ConnectionStatus Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            StatusChanged?.Invoke(this, value);
        }
    }

    /// <summary>Fires when the connection status changes.</summary>
    public event EventHandler<ConnectionStatus>? StatusChanged;
}

public enum ConnectionStatus
{
    Connected,
    Reconnecting,
    Disconnected,
}
