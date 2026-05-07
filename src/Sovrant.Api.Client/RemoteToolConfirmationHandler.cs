using System.Text.Json;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Client.Remote;

/// <summary>
/// <see cref="IToolConfirmationHandler"/> that relays confirmation requests
/// to the user via the UI and forwards the response back to the server via SignalR.
/// </summary>
public sealed class RemoteToolConfirmationHandler : IToolConfirmationHandler
{
    private readonly SignalRStreamingClient _signalR;

    /// <summary>
    /// Fires when the server requests tool confirmation. The UI subscribes to this
    /// to show the confirmation dialog.
    /// </summary>
    public event Func<string, JsonElement, Task<ConfirmationDecision>>? ConfirmationRequested;

    public RemoteToolConfirmationHandler(SignalRStreamingClient signalR)
    {
        _signalR = signalR;
    }

    public async Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        if (ConfirmationRequested is not null)
        {
            var decision = await ConfirmationRequested.Invoke(toolName, input);
            return decision;
        }

        // No UI handler — default to deny for safety.
        return ConfirmationDecision.Deny;
    }
}
