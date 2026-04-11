using System.Text.Json;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Web.Adapters;

/// <summary>
/// Blazor confirmation handler that raises an event for the Chat component
/// to show an inline approve/deny dialog, then waits for the user's response.
/// </summary>
public sealed class BlazorConfirmationHandler : IToolConfirmationHandler
{
    public event Action<ConfirmationRequest>? ConfirmationRequested;

    public async Task<bool> RequestConfirmationAsync(string toolName, JsonElement input, CancellationToken ct)
    {
        if (ConfirmationRequested is null)
            return false;

        var request = new ConfirmationRequest(toolName, input);
        ConfirmationRequested.Invoke(request);

        using var reg = ct.Register(() => request.Deny());
        return await request.Task;
    }
}

public sealed class ConfirmationRequest
{
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public string ToolName { get; }
    public JsonElement Input { get; }
    public Task<bool> Task => _tcs.Task;

    public ConfirmationRequest(string toolName, JsonElement input)
    {
        ToolName = toolName;
        Input = input;
    }

    public void Approve() => _tcs.TrySetResult(true);
    public void Deny() => _tcs.TrySetResult(false);
}
