using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Engine;

/// <summary>
/// Default <see cref="IEngineRecovery"/>. Idempotent: calling
/// <see cref="RecoverAsync"/> twice is a no-op on the second call
/// because the first call writes the missing close-out rows and
/// thereafter <see cref="IRuntimeTraceStore.ListInFlightRunsAsync"/>
/// returns an empty list.
/// </summary>
public sealed partial class EngineRecovery : IEngineRecovery
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "EngineRecovery: closing out {Count} in-flight runtime run(s) from previous process")]
    private static partial void LogInFlightDetected(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "EngineRecovery: closed out run {RunId} (plan {PlanId} v{PlanVersion}, orphaned step(s) {Steps})")]
    private static partial void LogRunClosed(ILogger logger, string runId, string planId, int planVersion, string steps);

    private readonly IRuntimeTraceStore _traceStore;
    private readonly ILogger<EngineRecovery> _logger;
    private readonly TimeProvider _clock;

    public EngineRecovery(
        IRuntimeTraceStore traceStore,
        ILogger<EngineRecovery>? logger = null,
        TimeProvider? clock = null)
    {
        _traceStore = traceStore ?? throw new ArgumentNullException(nameof(traceStore));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EngineRecovery>.Instance;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<RecoveredRun>> RecoverAsync(CancellationToken ct = default)
    {
        var inFlight = await _traceStore.ListInFlightRunsAsync(ct).ConfigureAwait(false);
        if (inFlight.Count == 0)
            return Array.Empty<RecoveredRun>();

        LogInFlightDetected(_logger, inFlight.Count);

        var recovered = new List<RecoveredRun>(inFlight.Count);
        foreach (var runId in inFlight)
        {
            var closed = await CloseOutRunAsync(runId, ct).ConfigureAwait(false);
            if (closed is not null)
                recovered.Add(closed);
        }

        return recovered;
    }

    private async Task<RecoveredRun?> CloseOutRunAsync(string runtimeRunId, CancellationToken ct)
    {
        var entries = await _traceStore.LoadAsync(runtimeRunId, ct).ConfigureAwait(false);
        if (entries.Count == 0)
            return null;

        // Find step_started rows without a matching step_completed/step_failed
        // on the same (plan_id, plan_version, step_index).
        var finished = new HashSet<(string PlanId, int PlanVersion, int StepIndex)>();
        var started = new List<RuntimeTraceEntry>();
        foreach (var entry in entries)
        {
            switch (entry.EntryType)
            {
                case "step_started":
                    started.Add(entry);
                    break;
                case "step_completed":
                case "step_failed":
                    if (entry.StepIndex.HasValue)
                        finished.Add((entry.PlanId, entry.PlanVersion, entry.StepIndex.Value));
                    break;
            }
        }

        var orphans = new List<RuntimeTraceEntry>();
        foreach (var s in started)
        {
            if (!s.StepIndex.HasValue) continue;
            var key = (s.PlanId, s.PlanVersion, s.StepIndex.Value);
            if (!finished.Contains(key))
                orphans.Add(s);
        }

        if (orphans.Count == 0)
            return null;

        // Write a synthetic step_failed for each orphan.
        var now = _clock.GetUtcNow();
        var orphanIndexes = new List<int>(orphans.Count);
        foreach (var orphan in orphans)
        {
            orphanIndexes.Add(orphan.StepIndex!.Value);
            var failedPayload = JsonSerializer.Serialize(new
            {
                status = StepStatus.Failed.ToString(),
                summary = "crashed: no completion recorded before process exit",
                matched_expectation = false,
                error = "EngineRecovery synthesised step_failed on startup",
            });
            await _traceStore.AppendAsync(new RuntimeTraceEntry(
                RuntimeRunId: orphan.RuntimeRunId,
                PlanId: orphan.PlanId,
                PlanVersion: orphan.PlanVersion,
                StepIndex: orphan.StepIndex,
                EntryType: "step_failed",
                Payload: failedPayload,
                Timestamp: now,
                SessionId: orphan.SessionId,
                WorkspaceId: orphan.WorkspaceId,
                ProjectId: orphan.ProjectId), ct).ConfigureAwait(false);
        }

        // And close the run itself out so trace readers see a terminal marker.
        var runCompletedPayload = JsonSerializer.Serialize(new
        {
            terminal_state = ExecutionTerminalState.Cancelled.ToString(),
            recovered_by = "EngineRecovery",
        });
        var last = orphans[^1];
        await _traceStore.AppendAsync(new RuntimeTraceEntry(
            RuntimeRunId: last.RuntimeRunId,
            PlanId: last.PlanId,
            PlanVersion: last.PlanVersion,
            StepIndex: null,
            EntryType: "run_completed",
            Payload: runCompletedPayload,
            Timestamp: now,
            SessionId: last.SessionId,
            WorkspaceId: last.WorkspaceId,
            ProjectId: last.ProjectId), ct).ConfigureAwait(false);

        LogRunClosed(
            _logger,
            runtimeRunId,
            last.PlanId,
            last.PlanVersion,
            string.Join(",", orphanIndexes));

        return new RecoveredRun(
            RuntimeRunId: runtimeRunId,
            PlanId: last.PlanId,
            PlanVersion: last.PlanVersion,
            OrphanedStepIndexes: orphanIndexes,
            SessionId: last.SessionId,
            WorkspaceId: last.WorkspaceId,
            ProjectId: last.ProjectId);
    }
}
