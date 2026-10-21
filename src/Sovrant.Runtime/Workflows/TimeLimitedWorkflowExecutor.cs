using System.Text.Json;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 145 A8.1 — holds every workflow run (Workflows page, API, scheduler) to the admin's run time
/// limit. When the limit is reached the run is stopped and the workflow is marked failed with the
/// reason in its history, instead of being left looking as if it were still running.
/// </summary>
public sealed class TimeLimitedWorkflowExecutor(
    IWorkflowExecutor inner,
    IWorkflowStore store,
    IWorkspaceSettingsStore? settings,
    WorkflowSessionNotifier? notifier = null,
    TimeProvider? clock = null,
    Sovrant.Runtime.Preferences.IUserPreferenceStore? prefs = null,
    Sovrant.Runtime.Providers.IProviderProfileStore? profiles = null) : IWorkflowExecutor
{
    public async Task<Workflow> RunAsync(string workflowId, CancellationToken ct = default)
    {
        var limit = RunLimits.Load(settings);
        using var timer = new CancellationTokenSource(limit, clock ?? TimeProvider.System);
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(ct, timer.Token);
        // A8.3: a security sign-out of the owner stops the run.
        var wf = await store.GetAsync(workflowId, ct).ConfigureAwait(false);
        var owner = wf?.OwnerUserId;
        using var running = Sovrant.Runtime.Auth.RunningWork.Track(owner, limited);
        // Cancelling the workflow stops this run (Phase 148).
        using var cancellable = WorkflowRuns.Track(workflowId, limited);
        // Started outside a chat (Workflows page, API, scheduler): run on the owner's model pick, not
        // the install default. Inside a chat, the conversation's context already carries it.
        var background = SessionContext.Current is null
            ? await Sovrant.Runtime.Providers.BackgroundModel.ContextForAsync(owner, wf?.WorkspaceId, wf?.SessionId, prefs, profiles, settings, ct).ConfigureAwait(false)
            : null;
        using var asOwner = background is null ? null : SessionContext.Push(background);
        try
        {
            return await inner.RunAsync(workflowId, limited.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellable.Cancelled && !ct.IsCancellationRequested)
        {
            // Cancelled by hand: the cancel already recorded it; return the workflow as it is now.
            return (await store.GetAsync(workflowId, CancellationToken.None).ConfigureAwait(false))!;
        }
        catch (OperationCanceledException) when (running.StoppedBySignOut && !ct.IsCancellationRequested)
        {
            return await StopAsync(workflowId, Sovrant.Runtime.Auth.RunningWork.StoppedMessage, "signed_out", WorkflowStatus.Cancelled).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return await StopAsync(workflowId, RunLimits.StoppedMessage(limit), "run_time_limit", WorkflowStatus.Failed).ConfigureAwait(false);
        }
    }

    private async Task<Workflow> StopAsync(string workflowId, string message, string reason, WorkflowStatus status)
    {
        // The run's own token is cancelled; record the outcome regardless.
        var none = CancellationToken.None;
        var wf = await store.GetAsync(workflowId, none).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"workflow {workflowId} not found");
        if (wf.Status is WorkflowStatus.Completed or WorkflowStatus.Failed or WorkflowStatus.Cancelled)
            return wf;
        await store.AppendEventAsync(wf.Id, status == WorkflowStatus.Cancelled ? WorkflowEventTypes.Cancelled : WorkflowEventTypes.Failed,
            JsonSerializer.Serialize(new { error = message, reason }),
            wf.WorkspaceId, wf.ProjectId, none).ConfigureAwait(false);
        await store.UpdateStateAsync(wf.Id, status, completedAt: DateTimeOffset.UtcNow, ct: none).ConfigureAwait(false);
        var stopped = (await store.GetAsync(wf.Id, none).ConfigureAwait(false))!;
        if (notifier is not null)
            await notifier.NotifyAsync(stopped, none).ConfigureAwait(false);
        return stopped;
    }
}
