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

        await PostAsync(workflow, "assistant", message, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds the workflow's linked session with its goal, framed as the
    /// user's own message, the moment the workflow is created — before any
    /// planning or step execution has happened. Without this, opening the
    /// chat link on a workflow that's still Planning (or a plain-created one
    /// that hasn't been picked up by the scheduler yet) shows nothing at
    /// all: step execution is the only thing that otherwise ever writes to
    /// this session, and planning uses a separate shared planner session,
    /// not the workflow's own. Called once, right after
    /// <see cref="IWorkflowStore.CreateAsync"/> returns, at every creation
    /// entry point (Web, Desktop, CLI, HTTP API, the Workflow tool).
    ///
    /// Only seeds when the session is actually empty. The Workflow tool lets
    /// a model pass its *own* current chat session id when spawning a
    /// workflow mid-conversation — that session already has the user's real
    /// message in it, and re-posting the goal as a synthetic "user" turn
    /// would insert a confusing duplicate into a real conversation.
    /// </summary>
    public async Task NotifyCreatedAsync(Workflow workflow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (string.IsNullOrEmpty(workflow.SessionId)) return;

        var existing = await _sessionStore.LoadAsync(workflow.SessionId, workflow.OwnerUserId, ct).ConfigureAwait(false);
        if (existing.Count > 0) return;

        await PostAsync(workflow, "user", workflow.Goal, ct).ConfigureAwait(false);
    }

    private async Task PostAsync(Workflow workflow, string role, string content, CancellationToken ct)
    {
        try
        {
            await _sessionStore.AppendAsync(
                workflow.SessionId!,
                new SessionEntry(
                    Id: $"wf-status-{Guid.NewGuid():N}",
                    Timestamp: DateTimeOffset.UtcNow,
                    Role: role,
                    Content: content),
                workflow.OwnerUserId,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotifyFailed(_logger, workflow.Id, ex.Message);
        }
    }
}
