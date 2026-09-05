using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Appends a status message to a workflow's linked chat session when the
/// workflow reaches a state the user should see next time they open that
/// chat — a terminal outcome or a pause for human review. This is the
/// "interact via chat" visibility gap-closer: today's pattern (ask in
/// chat, the model calls the Workflow tool) already surfaces progress
/// mid-conversation, but a workflow advanced later by the background
/// scheduler has no session open to speak into — this makes the outcome
/// visible next time the user reopens that chat instead. No-op for
/// workflows with no <see cref="Workflow.SessionId"/> (CLI/API-only runs).
/// </summary>
public sealed partial class WorkflowSessionNotifier
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WorkflowSessionNotifier: failed to post status message for workflow {WorkflowId}: {Error}")]
    private static partial void LogNotifyFailed(ILogger logger, string workflowId, string error);

    private readonly ISessionStore _sessionStore;
    private readonly ILogger<WorkflowSessionNotifier> _logger;

    public WorkflowSessionNotifier(ISessionStore sessionStore, ILogger<WorkflowSessionNotifier>? logger = null)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkflowSessionNotifier>.Instance;
    }

    /// <summary>
    /// Posts a status message for <paramref name="workflow"/> if it is linked
    /// to a session and its status is one the user should be told about.
    /// Failures are logged and swallowed — a session-store hiccup must not
    /// be mistaken for the workflow run itself having failed, since this is
    /// always called after the workflow's own terminal state is persisted.
    /// </summary>
    public async Task NotifyAsync(Workflow workflow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (string.IsNullOrEmpty(workflow.SessionId)) return;

        var message = workflow.Status switch
        {
            WorkflowStatus.Completed => $"Workflow completed: {workflow.Goal}",
            WorkflowStatus.Failed => $"Workflow failed: {workflow.Goal}",
            WorkflowStatus.AwaitingHuman => $"Workflow is awaiting your review: {workflow.Goal}",
            _ => null,
        };
        if (message is null) return;

        try
        {
            await _sessionStore.AppendAsync(
                workflow.SessionId,
                new SessionEntry(
                    Id: $"wf-status-{Guid.NewGuid():N}",
                    Timestamp: DateTimeOffset.UtcNow,
                    Role: "assistant",
                    Content: message),
                workflow.OwnerUserId,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(_logger, workflow.Id, ex.Message);
        }
    }
}
