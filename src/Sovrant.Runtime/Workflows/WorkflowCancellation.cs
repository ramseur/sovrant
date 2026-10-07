namespace Sovrant.Runtime.Workflows;

/// <summary>
/// One way to cancel a workflow, shared by the HTTP API and both apps: journal a
/// <see cref="WorkflowEventTypes.Cancelled"/> event, then move it to
/// <see cref="WorkflowStatus.Cancelled"/>. Terminal workflows are left untouched.
/// </summary>
public static class WorkflowCancellation
{
    public static bool IsTerminal(WorkflowStatus status) =>
        status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled;

    /// <summary>Cancels the workflow; returns false when it was already finished.</summary>
    public static async Task<bool> CancelAsync(this IWorkflowStore store, Workflow workflow, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(workflow);
        if (IsTerminal(workflow.Status))
            return false;
        await store.AppendEventAsync(workflow.Id, WorkflowEventTypes.Cancelled, "{}",
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);
        await store.UpdateStateAsync(workflow.Id, WorkflowStatus.Cancelled, completedAt: DateTimeOffset.UtcNow, ct: ct)
            .ConfigureAwait(false);
        // Stop its run too, if one is going here — otherwise it carried on, using the model, and
        // wrote its own status (Running, Completed, Failed) over the cancel.
        WorkflowRuns.Stop(workflow.Id);
        return true;
    }
}
