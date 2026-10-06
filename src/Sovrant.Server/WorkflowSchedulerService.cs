using System.Collections.Concurrent;
using Sovrant.Runtime.Workflows;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Server;

/// <summary>
/// Background service that keeps workflows moving without a human or agent
/// having to explicitly ask for the next step. Every poll tick, it lists
/// every <see cref="WorkflowStatus.Planning"/>/<see cref="WorkflowStatus.Running"/>
/// workflow (across all owners — this isn't scoped to one caller) and calls
/// <see cref="IWorkflowExecutor.RunAsync"/> on each, bounded by a concurrency
/// gate so it never fans out unbounded parallel LLM calls in one tick.
///
/// <see cref="WorkflowStatus.AwaitingHuman"/> and the terminal states are
/// deliberately excluded from the poll query — that's the entire mechanism
/// for not re-advancing a workflow that's waiting on a human decision or is
/// already done.
///
/// Startup/restart recovery needs no special code: <c>IWorkflowStore</c> is
/// backed by durable storage (SQLite/Postgres, not memory), and
/// <see cref="IWorkflowExecutor.RunAsync"/> is idempotent at the terminal-state
/// boundary. So whatever was <c>Planning</c>/<c>Running</c> before a restart
/// is simply present again on this service's very first tick — the DB is
/// already the source of truth, there's nothing to rehydrate.
/// </summary>
internal sealed partial class WorkflowSchedulerService : BackgroundService
{
    private const int DefaultPollSeconds = 20;
    private const int DefaultMaxConcurrent = 3;
    private const int ListLimit = 500;

    private readonly IWorkflowStore _store;
    private readonly IWorkflowExecutor _executor;
    private readonly ILogger<WorkflowSchedulerService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _maxConcurrent;

    /// <summary>
    /// Workflow IDs currently being advanced. Guards against double-advancing
    /// a workflow whose previous RunAsync call is still in flight when the
    /// next poll tick fires — an in-memory set is enough (self-heals on
    /// restart; nothing is ever "stuck" since the set starts empty).
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "WorkflowScheduler: tick advancing {Count} workflow(s)")]
    private static partial void LogTickStarted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WorkflowScheduler: failed to list due workflows: {Error}")]
    private static partial void LogListFailed(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "WorkflowScheduler: advancing workflow {WorkflowId} threw: {Error}")]
    private static partial void LogAdvanceFailed(ILogger logger, string workflowId, string error);

    public WorkflowSchedulerService(
        IWorkflowStore store,
        IWorkflowExecutor executor,
        IWorkspaceSettingsStore settings,
        ILogger<WorkflowSchedulerService> logger)
    {
        _store = store;
        _executor = executor;
        _logger = logger;

        var pollSeconds = ResolveInt(settings, WorkspaceSettingsKeys.WorkflowPollSeconds,
            "SOVRANT_WORKFLOW_POLL_SECONDS", DefaultPollSeconds);
        _pollInterval = TimeSpan.FromSeconds(pollSeconds);

        _maxConcurrent = ResolveInt(settings, WorkspaceSettingsKeys.WorkflowMaxConcurrent,
            "SOVRANT_WORKFLOW_MAX_CONCURRENT", DefaultMaxConcurrent);
    }

    /// <summary>The configured poll interval.</summary>
    public TimeSpan PollInterval => _pollInterval;

    /// <summary>The configured max concurrent advances per tick.</summary>
    public int MaxConcurrent => _maxConcurrent;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await TickAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<Workflow> planning;
        IReadOnlyList<Workflow> running;
        try
        {
            planning = await _store.ListAsync(ownerUserId: null, status: WorkflowStatus.Planning, limit: ListLimit, stoppingToken)
                .ConfigureAwait(false);
            running = await _store.ListAsync(ownerUserId: null, status: WorkflowStatus.Running, limit: ListLimit, stoppingToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogListFailed(_logger, ex.Message);
            return;
        }

        var due = planning.Concat(running)
            .Where(w => _inFlight.TryAdd(w.Id, 0))
            .ToList();

        if (due.Count == 0)
            return;

        LogTickStarted(_logger, due.Count);

        using var gate = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
#pragma warning disable CA2025 // Task.WhenAll below ensures all tasks complete before gate is disposed
        var tasks = due.Select(w => AdvanceOneAsync(w.Id, gate, stoppingToken));
#pragma warning restore CA2025
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task AdvanceOneAsync(string workflowId, SemaphoreSlim gate, CancellationToken stoppingToken)
    {
        try
        {
            await gate.WaitAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown fired before this workflow got a turn — nothing
            // started yet, so there's nothing to let finish.
            _inFlight.TryRemove(workflowId, out _);
            return;
        }

        try
        {
            // Deliberately CancellationToken.None, not stoppingToken: once a
            // workflow has actually started advancing, a shutdown signal
            // must not abort it mid-step and leave it in an inconsistent
            // state. The host's shutdown timeout gives this a grace window.
            await _executor.RunAsync(workflowId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAdvanceFailed(_logger, workflowId, ex.Message);
        }
        finally
        {
            gate.Release();
            _inFlight.TryRemove(workflowId, out _);
        }
    }

    private static int ResolveInt(IWorkspaceSettingsStore settings, string settingsKey, string envVar, int fallback)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable(envVar), out var fromEnv))
            return fromEnv;

        var fromDb = settings.GetGlobalAsync(settingsKey).GetAwaiter().GetResult();
        if (int.TryParse(fromDb, out var parsed))
            return parsed;

        return fallback;
    }
}
