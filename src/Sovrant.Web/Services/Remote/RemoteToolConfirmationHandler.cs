using System.Text.Json;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Web.Services.Remote;

/// <summary>
/// <see cref="IToolConfirmationHandler"/> that relays confirmation requests
/// to the user via the Blazor UI (same event pattern as <c>BlazorConfirmationHandler</c>)
/// but with an additional path to forward the response back to the server via SignalR.
/// </summary>
public sealed class RemoteToolConfirmationHandler : IToolConfirmationHandler
{
    private readonly SignalRStreamingClient _signalR;

    /// <summary>
    /// Fires when the server requests tool confirmation. The UI subscribes to this
    /// to show the confirmation dialog. The returned decision is forwarded back
    /// to the server so the executor can either run the tool, remember the
    /// approval for the rest of the turn, or deny the call.
    /// </summary>
    public event Func<string, JsonElement, Task<ConfirmationDecision>>? ConfirmationRequested;

    public RemoteToolConfirmationHandler(SignalRStreamingClient signalR)
    {
        _signalR = signalR;
    }

    public async Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        // If the UI has subscribed, show the dialog and relay the answer.
        if (ConfirmationRequested is not null)
        {
            var decision = await ConfirmationRequested.Invoke(toolName, input);
            return decision;
        }

        // No UI handler — default to deny for safety.
        return ConfirmationDecision.Deny;
    }
}
