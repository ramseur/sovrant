using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Desktop.Adapters;

/// <summary>
/// Desktop confirmation handler that shows tool calls inline in the chat
/// and waits for user approval when the permission policy requires it.
/// When no subscriber is attached (e.g. during background operations),
/// falls back to auto-approve for non-destructive tools and deny for destructive ones.
/// </summary>
public sealed class DesktopConfirmationHandler : IToolConfirmationHandler
{
    /// <summary>
    /// Raised when a tool needs user confirmation. The handler should call
    /// <see cref="ConfirmationRequest.Approve"/>, <see cref="ConfirmationRequest.ApproveForTurn"/>,
    /// or <see cref="ConfirmationRequest.Deny"/> to unblock the tool executor.
    /// </summary>
    public event Action<ConfirmationRequest>? ConfirmationRequested;

    public async Task<ConfirmationDecision> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        if (ConfirmationRequested is null)
        {
            // No UI subscriber — deny by default for safety.
            return ConfirmationDecision.Deny;
        }

        var request = new ConfirmationRequest(toolName, input);
        ConfirmationRequested.Invoke(request);

        // Wait for the user to approve or deny.
        using var reg = ct.Register(() => request.Deny());
        return await request.Task;
    }
}

/// <summary>
/// Represents a pending tool confirmation request shown inline in the chat.
/// </summary>
public sealed class ConfirmationRequest
{
    private readonly TaskCompletionSource<ConfirmationDecision> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string ToolName { get; }
    public JsonElement Input { get; }
    public Task<ConfirmationDecision> Task => _tcs.Task;

    public ConfirmationRequest(string toolName, JsonElement input)
    {
        ToolName = toolName;
        Input = input;
    }

    public void Approve() => _tcs.TrySetResult(ConfirmationDecision.AllowOnce);
    public void ApproveForTurn() => _tcs.TrySetResult(ConfirmationDecision.AllowForTurn);
    public void Deny() => _tcs.TrySetResult(ConfirmationDecision.Deny);
}
