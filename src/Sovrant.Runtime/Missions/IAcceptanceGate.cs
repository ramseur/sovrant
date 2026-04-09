using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 51 — policy seam that decides whether a completed engine run
/// satisfies a mission's acceptance criteria. Called by the mission
/// executor after <see cref="IExecutor.ExecuteAsync"/> returns with
/// <see cref="ExecutionTerminalState.Completed"/>. Implementations can
/// consult the run outcomes, the mission goal, and the existing event
/// journal to decide.
/// </summary>
public interface IAcceptanceGate
{
    Task<AcceptanceDecision> EvaluateAsync(
        Mission mission,
        ExecutionResult result,
        CancellationToken ct = default);
}

/// <param name="Accepted">True if the mission can be marked completed; false if the gate rejects and the mission should re-plan or escalate to human review.</param>
/// <param name="Reason">Human-readable reason — written into the mission event payload for the audit trail.</param>
/// <param name="RequiresHuman">If true, the mission transitions to <see cref="MissionStatus.AwaitingHuman"/> rather than being auto-accepted or rejected.</param>
public sealed record AcceptanceDecision(bool Accepted, string Reason, bool RequiresHuman = false);

/// <summary>
/// Default acceptance gate: accept if every step in the run succeeded
/// and no step was marked Contradicted. Good enough to prove the gate
/// seam is wired; production missions can swap in an LLM reviewer or
/// per-mission policy.
/// </summary>
public sealed class AllStepsSucceededGate : IAcceptanceGate
{
    public Task<AcceptanceDecision> EvaluateAsync(
        Mission mission,
        ExecutionResult result,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.TerminalState != ExecutionTerminalState.Completed)
        {
            return Task.FromResult(new AcceptanceDecision(
                Accepted: false,
                Reason: $"run terminal state was {result.TerminalState}"));
        }

        foreach (var outcome in result.Outcomes)
        {
            if (outcome.Status != StepStatus.Succeeded)
            {
                return Task.FromResult(new AcceptanceDecision(
                    Accepted: false,
                    Reason: $"step {outcome.StepIndex} ended {outcome.Status}"));
            }
        }

        return Task.FromResult(new AcceptanceDecision(
            Accepted: true,
            Reason: "all steps succeeded"));
    }
}
