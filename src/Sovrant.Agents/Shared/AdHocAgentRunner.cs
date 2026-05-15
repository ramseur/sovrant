using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Storage;

namespace Sovrant.Agents.Shared;

/// <summary>
/// Phase 90 Track C — kicks off a single agent template against a one-off prompt
/// and records the run in the unified agent_runs ledger so it surfaces in the
/// Command Center, Activity, and "Recent runs" views. Sits below the heavyweight
/// <see cref="Sovrant.Agents.Orchestration.IAgentOrchestrator"/> (which forces a
/// team / decomposition pipeline) and above <see cref="SovrantAgentFactory"/>
/// (which is unledgered).
/// </summary>
public sealed class AdHocAgentRunner
{
    private readonly AgentTemplateRegistry _registry;
    private readonly SovrantAgentFactory _factory;
    private readonly IAgentRunStore _runStore;
    private readonly ILogger<AdHocAgentRunner> _logger;

    public AdHocAgentRunner(
        AgentTemplateRegistry registry,
        SovrantAgentFactory factory,
        IAgentRunStore runStore,
        ILogger<AdHocAgentRunner> logger)
    {
        _registry = registry;
        _factory = factory;
        _runStore = runStore;
        _logger = logger;
    }

    public async Task<AdHocAgentRunResult> RunAsync(
        string templateName,
        string prompt,
        string workspaceId,
        string? projectId,
        string userId,
        CancellationToken ct = default)
    {
        var template = _registry.TryGet(templateName);
        if (template is null)
            return new AdHocAgentRunResult(RunId: string.Empty, Success: false, Output: string.Empty,
                Error: $"Unknown template '{templateName}'.");

        var runId = $"run-{Guid.NewGuid():N}";
        var startedAt = DateTimeOffset.UtcNow;

        await _runStore.CreateAsync(new AgentRunRecord(
            RunId: runId,
            ParentRunId: null,
            TeamId: null,
            MemberId: templateName,
            WorkspaceId: workspaceId,
            ProjectId: projectId,
            UserId: userId,
            Kind: "adhoc-template",
            Status: "running",
            StartedAt: startedAt), ct).ConfigureAwait(false);

        try
        {
            var agent = _factory.Create(template);
            // Bind the runtime's session id to the run id so the agent's turns
            // are persisted under the same id as the agent_runs ledger row —
            // /activity?run={runId} can then resolve to the agent's session.
            await agent.InitializeSessionAsync(runId, userId, ct).ConfigureAwait(false);
            var taskId = Guid.NewGuid().ToString("N");
            var result = await agent.HandleAsync(new AgentTask(taskId, prompt), ct).ConfigureAwait(false);

            await _runStore.UpdateStatusAsync(
                runId,
                result.Success ? "succeeded" : "failed",
                inputTokens: 0,
                outputTokens: 0,
                costUsd: null,
                ct).ConfigureAwait(false);

            return new AdHocAgentRunResult(
                RunId: runId,
                Success: result.Success,
                Output: result.Output,
                Error: result.Error);
        }
        catch (OperationCanceledException)
        {
            await _runStore.UpdateStatusAsync(runId, "cancelled", ct: CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (LogAndMark(ex, runId))
        {
            return new AdHocAgentRunResult(
                RunId: runId,
                Success: false,
                Output: string.Empty,
                Error: ex.Message);
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Ad-hoc runs must always end with the ledger row updated and a clean result returned to the UI. Any agent-side exception is captured and surfaced as the run's Error.")]
    private bool LogAndMark(Exception ex, string runId)
    {
        LogRunFailed(_logger, runId, ex.Message, ex);
        try { _runStore.UpdateStatusAsync(runId, "failed", ct: CancellationToken.None).GetAwaiter().GetResult(); }
        catch (Exception inner) { LogLedgerWriteFailed(_logger, runId, inner.Message, inner); }
        return true;
    }

    private static readonly Action<ILogger, string, string, Exception?> _logRunFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(1, "AdHocRunFailed"),
            "Ad-hoc agent run {RunId} failed: {Error}");
    private static void LogRunFailed(ILogger logger, string runId, string error, Exception? ex) =>
        _logRunFailed(logger, runId, error, ex);

    private static readonly Action<ILogger, string, string, Exception?> _logLedgerWriteFailed =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2, "AdHocLedgerWriteFailed"),
            "Failed to update agent_runs ledger for {RunId}: {Error}");
    private static void LogLedgerWriteFailed(ILogger logger, string runId, string error, Exception? ex) =>
        _logLedgerWriteFailed(logger, runId, error, ex);
}

public sealed record AdHocAgentRunResult(
    string RunId,
    bool Success,
    string Output,
    string? Error);
