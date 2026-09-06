using System.Text.Json;
using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Orchestrates the "review before run" flow: generate a plan up front so a
/// human can inspect and edit it before anything executes, instead of only
/// ever planning and executing in the same breath the way a bare
/// <see cref="IWorkflowExecutor.RunAsync"/> call does. Lands the workflow in
/// <see cref="WorkflowStatus.AwaitingHuman"/> rather than
/// <see cref="WorkflowStatus.Planning"/> so the background scheduler --
/// which polls exactly <c>Planning</c>/<c>Running</c> -- never picks the
/// workflow up and starts executing it out from under a user mid-review.
/// </summary>
public sealed class WorkflowPlanningService
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowPlanner _planner;

    public WorkflowPlanningService(IWorkflowStore store, IWorkflowPlanner planner)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    /// <summary>
    /// Creates a new workflow and immediately asks the planner to decompose
    /// the goal, landing the result in <see cref="WorkflowStatus.AwaitingHuman"/>
    /// for review. Calling <c>IWorkflowExecutor.RunAsync</c> afterward reuses
    /// this exact plan instead of generating a new one, so edits made via
    /// <see cref="SavePlanAsync"/> before running are preserved.
    /// </summary>
    public async Task<Workflow> GenerateAsync(
        string goal,
        string? sessionId = null,
        string? workspaceId = null,
        string? projectId = null,
        string? ownerUserId = null,
        CancellationToken ct = default)
    {
        var workflow = await _store.CreateAsync(goal, sessionId, workspaceId, projectId, ownerUserId, ct)
            .ConfigureAwait(false);
        var plan = await _planner.PlanAsync(workflow, ct).ConfigureAwait(false);

        await _store.UpdateStateAsync(
            workflow.Id, WorkflowStatus.AwaitingHuman,
            planJson: WorkflowPlanJson.Serialize(plan), ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(
            workflow.Id,
            WorkflowEventTypes.PlanRevised,
            JsonSerializer.Serialize(new { plan_id = plan.Id, plan_version = plan.PlanVersion }),
            workspaceId, projectId, ct).ConfigureAwait(false);

        return (await _store.GetAsync(workflow.Id, ct).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Persists a human-edited plan for a workflow that has never actually
    /// run yet. Throws if the workflow has already started executing --
    /// at that point the plan is execution history, not a draft, and
    /// editing it would rewrite the record of what actually happened.
    /// </summary>
    public async Task<Workflow> SavePlanAsync(
        string workflowId,
        IReadOnlyList<RuntimeStep> steps,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
            throw new ArgumentException("a plan needs at least one step", nameof(steps));

        var workflow = await _store.GetAsync(workflowId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow {workflowId} not found");

        var events = await _store.GetEventsAsync(workflowId, ct).ConfigureAwait(false);
        if (events.Any(e => e.EventType == WorkflowEventTypes.RunStarted))
            throw new InvalidOperationException("cannot edit the plan for a workflow that has already run");

        var plan = new RuntimePlan(
            Id: $"plan-{Guid.NewGuid():N}",
            PlanVersion: 1,
            Goal: workflow.Goal,
            Steps: steps.Select((s, i) => s with { Index = i }).ToList(),
            CreatedAt: DateTimeOffset.UtcNow);

        await _store.UpdateStateAsync(
            workflowId, WorkflowStatus.AwaitingHuman,
            planJson: WorkflowPlanJson.Serialize(plan), ct: ct).ConfigureAwait(false);
        await _store.AppendEventAsync(
            workflowId,
            WorkflowEventTypes.PlanRevised,
            JsonSerializer.Serialize(new { plan_id = plan.Id, plan_version = plan.PlanVersion, edited = true }),
            workflow.WorkspaceId, workflow.ProjectId, ct).ConfigureAwait(false);

        return (await _store.GetAsync(workflowId, ct).ConfigureAwait(false))!;
    }
}
