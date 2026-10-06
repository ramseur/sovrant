namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 51 — runs one workflow forward by one engine cycle:
/// plan → execute → acceptance gate → journal → terminal state.
/// Idempotent at the workflow-state boundary: calling <see cref="RunAsync"/>
/// twice on an already-completed workflow is a no-op.
/// </summary>
public interface IWorkflowExecutor
{
    Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default);
}
