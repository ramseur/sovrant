using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Teams;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Tools;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Core swarm engine. Executes a <see cref="SwarmPlan"/> wave-by-wave with concurrency control,
/// file locking, token budget tracking, retry logic, and event emission.
/// </summary>
public sealed partial class SwarmOrchestrator : ISwarmOrchestrator
{
    private readonly SovrantAgentFactory _factory;
    private readonly AgentTemplateRegistry _templates;
    private readonly ITeamRegistry _teamRegistry;
    private readonly IFileLockManager _fileLockManager;
    private readonly SwarmStateTracker _stateTracker;
    private readonly SwarmSession _session;
    private readonly IToolRegistry _toolRegistry;
    private readonly WorkspaceContext _workspace;
    private readonly ILogger<SwarmOrchestrator> _logger;

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting swarm '{SwarmId}' — {TaskCount} tasks, {WaveCount} waves")]
    private static partial void LogSwarmStart(ILogger logger, string swarmId, int taskCount, int waveCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing wave {Wave} with {TaskCount} tasks")]
    private static partial void LogWaveStart(ILogger logger, int wave, int taskCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Task '{TaskId}' started on agent '{AgentName}'")]
    private static partial void LogTaskStart(ILogger logger, string taskId, string agentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task '{TaskId}' completed ({TokensUsed} tokens)")]
    private static partial void LogTaskComplete(ILogger logger, string taskId, int tokensUsed);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task '{TaskId}' failed (attempt {Attempt}): {Error}")]
    private static partial void LogTaskFailed(ILogger logger, string taskId, int attempt, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task '{TaskId}' blocked — file '{FilePath}' locked by '{HeldBy}'")]
    private static partial void LogFileConflict(ILogger logger, string taskId, string filePath, string heldBy);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token budget exceeded for swarm '{SwarmId}': {Used}/{Limit}")]
    private static partial void LogBudgetExceeded(ILogger logger, string swarmId, int used, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Swarm '{SwarmId}' completed with status '{Status}' in {DurationMs}ms")]
    private static partial void LogSwarmComplete(ILogger logger, string swarmId, SwarmStatus status, long durationMs);

    public SwarmOrchestrator(
        SovrantAgentFactory factory,
        AgentTemplateRegistry templates,
        ITeamRegistry teamRegistry,
        IFileLockManager fileLockManager,
        SwarmStateTracker stateTracker,
        SwarmSession session,
        IToolRegistry toolRegistry,
        WorkspaceContext workspace,
        ILogger<SwarmOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(teamRegistry);
        ArgumentNullException.ThrowIfNull(fileLockManager);
        ArgumentNullException.ThrowIfNull(stateTracker);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(toolRegistry);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(logger);
        _factory = factory;
        _templates = templates;
        _teamRegistry = teamRegistry;
        _fileLockManager = fileLockManager;
        _stateTracker = stateTracker;
        _session = session;
        _toolRegistry = toolRegistry;
        _workspace = workspace;
        _logger = logger;
    }

    /// <summary>
    /// Executes a swarm plan end-to-end. Emits events via <paramref name="onEvent"/> callback.
    /// </summary>
    /// <param name="plan">The decomposed plan to execute.</param>
    /// <param name="config">Swarm configuration.</param>
    /// <param name="onEvent">Optional event callback (used by SSE streaming).</param>
    /// <param name="executionContext">
    /// Identifies the user / workspace / project that owns this run. Every
    /// persisted swarm event is stamped with the workspace and project IDs
    /// from this context, which is what enables per-workspace and per-project
    /// queries on the <c>swarm_events</c> table. Pass <c>null</c> from CLI
    /// callers that don't have a workspace context yet.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<SwarmResult> ExecuteAsync(
        SwarmPlan plan,
        SwarmConfig config,
        Action<SwarmEvent>? onEvent = null,
        SwarmExecutionContext? executionContext = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(config);

        var sw = Stopwatch.StartNew();
        var tokenCounter = new TokenCounter();
        var swarmContext = executionContext ?? SwarmExecutionContext.Empty;

        // Resolve the artifact directory for this swarm's outputs.
        // Phase 53: uses scoped store when available, falls back to legacy layout.
        var artifactsDir = await _workspace.GetArtifactsDirectoryAsync(ct).ConfigureAwait(false);

        var result = new SwarmResult
        {
            SwarmId = plan.Id,
            Status = SwarmStatus.Executing,
            Tasks = plan.Tasks,
        };

        _stateTracker.Update(plan.Id, result);

        LogSwarmStart(_logger, plan.Id, plan.Tasks.Count, plan.WaveCount);
        await EmitAsync(new SwarmEvent.PlanCreated(plan.Id, plan.Tasks.Count, plan.WaveCount), onEvent, swarmContext, ct).ConfigureAwait(false);

        var semaphore = new SemaphoreSlim(config.MaxConcurrent, config.MaxConcurrent);
        try
        {

            for (var wave = 0; wave < plan.WaveCount; wave++)
            {
                ct.ThrowIfCancellationRequested();

                var waveTasks = plan.GetWave(wave);
                LogWaveStart(_logger, wave, waveTasks.Count);

                var execTasks = new List<Task>(waveTasks.Count);

                foreach (var node in waveTasks)
                {
                    // Skip tasks whose dependencies failed
                    if (HasFailedDependency(node, plan.Tasks))
                    {
                        node.Status = SwarmTaskStatus.Cancelled;
                        node.Error = "Cancelled — a dependency failed.";
                        continue;
                    }

#pragma warning disable CA2025 // Semaphore is disposed after Task.WhenAll ensures all tasks complete
                    execTasks.Add(ExecuteTaskAsync(node, plan, config, semaphore, tokenCounter, artifactsDir, onEvent, swarmContext, ct));
#pragma warning restore CA2025
                }

                await Task.WhenAll(execTasks).ConfigureAwait(false);

                // Phase 57 — emit WaveCompleted event
                var waveCompleted = waveTasks.Count(t => t.Status == SwarmTaskStatus.Completed);
                var waveFailed = waveTasks.Count(t => t.Status == SwarmTaskStatus.Failed);
                await EmitAsync(
                    new SwarmEvent.WaveCompleted(plan.Id, wave, waveCompleted, waveFailed),
                    onEvent, swarmContext, ct).ConfigureAwait(false);

                // Check budget after each wave
                if (tokenCounter.Value > config.MaxTokenBudget)
                {
                    LogBudgetExceeded(_logger, plan.Id, tokenCounter.Value, config.MaxTokenBudget);
                    await EmitAsync(new SwarmEvent.BudgetExceeded(plan.Id, tokenCounter.Value, config.MaxTokenBudget), onEvent, swarmContext, ct).ConfigureAwait(false);

                    // Cancel remaining waves
                    CancelRemainingTasks(plan.Tasks, wave + 1);
                    break;
                }
            }

            // Build combined output
            result.CombinedOutput = BuildCombinedOutput(plan.Tasks);
            result.TotalTokensUsed = tokenCounter.Value;
            result.Duration = sw.Elapsed;

            // Determine final status
            result.Status = plan.Tasks.Any(t => t.Status == SwarmTaskStatus.Failed)
                ? SwarmStatus.Failed
                : SwarmStatus.Completed;

            _stateTracker.Update(plan.Id, result);
        }
        catch (OperationCanceledException)
        {
            // Mark any still-running tasks as cancelled so status is accurate.
            foreach (var task in plan.Tasks)
            {
                if (task.Status == SwarmTaskStatus.Running)
                    task.Status = SwarmTaskStatus.Cancelled;
            }
            result.Status = SwarmStatus.Cancelled;
            result.TotalTokensUsed = tokenCounter.Value;
            result.Duration = sw.Elapsed;
            _stateTracker.Update(plan.Id, result);
        }
        finally
        {
            _fileLockManager.Clear();
            semaphore.Dispose();

            sw.Stop();
            LogSwarmComplete(_logger, plan.Id, result.Status, sw.ElapsedMilliseconds);
            await EmitAsync(
                new SwarmEvent.SwarmCompleted(plan.Id, result.Status, tokenCounter.Value, sw.Elapsed.TotalSeconds),
                onEvent, swarmContext, ct).ConfigureAwait(false);
        }

        return result;
    }

    private async Task ExecuteTaskAsync(
        SwarmTaskNode node,
        SwarmPlan plan,
        SwarmConfig config,
        SemaphoreSlim semaphore,
        TokenCounter tokenCounter,
        string artifactsDir,
        Action<SwarmEvent>? onEvent,
        SwarmExecutionContext swarmContext,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ExecuteTaskCoreAsync(node, plan, config, tokenCounter, artifactsDir, onEvent, swarmContext, ct).ConfigureAwait(false);
        }
        finally
        {
            semaphore.Release();
            _fileLockManager.ReleaseAll(node.Id);
        }
    }

    private async Task ExecuteTaskCoreAsync(
        SwarmTaskNode node,
        SwarmPlan plan,
        SwarmConfig config,
        TokenCounter tokenCounter,
        string artifactsDir,
        Action<SwarmEvent>? onEvent,
        SwarmExecutionContext swarmContext,
        CancellationToken ct)
    {
        var agentName = ResolveAgentName(node, config);

        // Acquire file locks (skipped when disabled on this run's config).
        if (config.FileLocksEnabled)
        {
            foreach (var file in node.FilesToModify)
            {
                if (_fileLockManager.IsLockedByOther(file, node.Id))
                {
                    var holderTaskId = _fileLockManager.GetHolder(file) ?? "unknown";
                    // Look up the holder's agent name from the plan.
                    var holderNode = plan.Tasks.FirstOrDefault(t => t.Id == holderTaskId);
                    var holderAgentName = holderNode is not null ? ResolveAgentName(holderNode, config) : holderTaskId;

                    LogFileConflict(_logger, node.Id, file, holderTaskId);
                    await EmitAsync(new SwarmEvent.FileConflict(plan.Id, node.Id, agentName, file, holderTaskId, holderAgentName), onEvent, swarmContext, ct).ConfigureAwait(false);

                    node.Status = SwarmTaskStatus.Blocked;
                    node.Error = $"File '{file}' locked by task '{holderTaskId}'.";
                    return;
                }
                _fileLockManager.TryAcquire(file, node.Id);
            }
        }
        node.Status = SwarmTaskStatus.Running;
        LogTaskStart(_logger, node.Id, agentName);
        await EmitAsync(new SwarmEvent.TaskStarted(plan.Id, node.Id, agentName, node.Description, node.Wave), onEvent, swarmContext, ct).ConfigureAwait(false);

        var attempt = 0;
        while (attempt <= config.MaxRetries)
        {
            try
            {
                var agent = CreateAgent(node, plan, config);
                var prompt = BuildTaskPrompt(node, plan, artifactsDir);

                using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                taskCts.CancelAfter(TimeSpan.FromSeconds(config.TaskTimeoutSeconds));

                var agentTask = AgentTask.Create(prompt, agentName);
                var result = await agent.HandleAsync(agentTask, taskCts.Token).ConfigureAwait(false);

                if (result.Success)
                {
                    node.Status = SwarmTaskStatus.Completed;
                    node.Output = result.Output;
                    // Estimate tokens — actual tracking would require runtime event inspection
                    var estimatedTokens = (prompt.Length + result.Output.Length) / 4;
                    node.TokensUsed = estimatedTokens;
                    tokenCounter.Add(estimatedTokens);

                    LogTaskComplete(_logger, node.Id, estimatedTokens);
                    await EmitAsync(new SwarmEvent.TaskCompleted(plan.Id, node.Id, agentName, node.Description, result.Output, estimatedTokens), onEvent, swarmContext, ct).ConfigureAwait(false);
                    return;
                }

                // Agent returned failure
                attempt++;
                node.RetryCount = attempt;
                LogTaskFailed(_logger, node.Id, attempt, result.Error ?? "Unknown error");

                if (attempt > config.MaxRetries)
                {
                    node.Status = SwarmTaskStatus.Failed;
                    node.Error = result.Error ?? "Agent returned failure.";
                    await EmitAsync(new SwarmEvent.TaskFailed(plan.Id, node.Id, agentName, node.Description, node.Error, attempt), onEvent, swarmContext, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Per-task timeout, not swarm cancellation
                attempt++;
                node.RetryCount = attempt;
                LogTaskFailed(_logger, node.Id, attempt, "Task timed out");

                if (attempt > config.MaxRetries)
                {
                    node.Status = SwarmTaskStatus.Failed;
                    node.Error = "Task timed out.";
                    await EmitAsync(new SwarmEvent.TaskFailed(plan.Id, node.Id, agentName, node.Description, node.Error, attempt), onEvent, swarmContext, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private SovrantAgent CreateAgent(SwarmTaskNode node, SwarmPlan plan, SwarmConfig config)
    {
        // Each swarm task gets its own file-lock-aware executor so concurrent agents
        // cannot write to the same file. Auto-acquires locks for undeclared writes.
        // When permissions are set to "yolo" or "accept-edits", bypass confirmation prompts.
        var bypass = config.Permissions.Equals("yolo", StringComparison.OrdinalIgnoreCase)
                  || config.Permissions.Equals("accept-edits", StringComparison.OrdinalIgnoreCase);
        var innerExecutor = _factory.GetDefaultExecutor();
        var swarmExecutor = new SwarmToolExecutor(innerExecutor, _fileLockManager, node.Id, bypass, bypass ? _toolRegistry : null);

        // Priority 1: If the plan references a team, try to find a matching team member.
        if (plan.TeamId is not null)
        {
            var agentName = ResolveAgentName(node, config);
            var teamMembers = _teamRegistry.GetAllMembers();
            var member = teamMembers.FirstOrDefault(m =>
                m.Name.Equals(agentName, StringComparison.OrdinalIgnoreCase));
            if (member is not null)
                return _factory.Create(member, swarmExecutor);
        }

        // Priority 2: Resolve from agent templates (built-in or user-defined)
        var templateName = node.AgentTemplate;
        if (templateName is not null && config.TemplateOverrides.TryGetValue(templateName, out var overridden))
            templateName = overridden;

        templateName ??= "coder";

        var template = _templates.TryGet(templateName);
        if (template is not null)
            return _factory.Create(template, executorOverride: swarmExecutor);

        // Priority 3: Fallback to raw parameters
        return _factory.Create(
            name: $"swarm-worker-{node.Id}",
            role: AgentRole.Coder,
            allowedTools: node.AllowedTools?.ToList(),
            executorOverride: swarmExecutor);
    }

    private static string BuildTaskPrompt(SwarmTaskNode node, SwarmPlan plan, string artifactsDir)
    {
        var sb = new StringBuilder();
        sb.Append("## Task: ").AppendLine(node.Description);
        sb.AppendLine();

        // Tell the agent where to write output files
        sb.Append("### Output directory: ").AppendLine(artifactsDir);
        sb.AppendLine("All files you create or modify should be placed in this directory unless the task explicitly references an existing file elsewhere.");
        sb.AppendLine();

        // Include predecessor outputs as context
        if (node.Dependencies.Count > 0)
        {
            sb.AppendLine("### Context from prerequisite tasks:");
            foreach (var depId in node.Dependencies)
            {
                var dep = plan.Tasks.FirstOrDefault(t => t.Id == depId);
                if (dep?.Output is not null)
                {
                    sb.Append("**").Append(depId).Append("** (").Append(dep.Description).AppendLine("):");
                    sb.AppendLine(dep.Output);
                    sb.AppendLine();
                }
            }
        }

        if (node.FilesToModify.Count > 0)
        {
            sb.Append("### Files to modify: ").AppendLine(string.Join(", ", node.FilesToModify));
        }

        sb.AppendLine();
        sb.Append("Original goal: ").AppendLine(plan.OriginalPrompt);

        return sb.ToString();
    }

    private static string ResolveAgentName(SwarmTaskNode node, SwarmConfig config)
    {
        var name = node.AgentTemplate;
        if (name is not null && config.TemplateOverrides.TryGetValue(name, out var overridden))
            return overridden;
        return name ?? "coder";
    }

    private static bool HasFailedDependency(SwarmTaskNode node, IList<SwarmTaskNode> allTasks)
    {
        return node.Dependencies.Any(depId =>
        {
            var dep = allTasks.FirstOrDefault(t => t.Id == depId);
            return dep is not null && dep.Status is SwarmTaskStatus.Failed or SwarmTaskStatus.Cancelled;
        });
    }

    private static void CancelRemainingTasks(IList<SwarmTaskNode> tasks, int fromWave)
    {
        foreach (var task in tasks)
        {
            if (task.Wave >= fromWave && task.Status == SwarmTaskStatus.Pending)
            {
                task.Status = SwarmTaskStatus.Cancelled;
                task.Error = "Cancelled — token budget exceeded.";
            }
        }
    }

    private static string BuildCombinedOutput(IList<SwarmTaskNode> tasks)
    {
        var sb = new StringBuilder();
        foreach (var task in tasks.Where(t => t.Status == SwarmTaskStatus.Completed && t.Output is not null))
        {
            sb.Append("## ").Append(task.Id).Append(": ").AppendLine(task.Description);
            sb.AppendLine(task.Output);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task EmitAsync(
        SwarmEvent evt,
        Action<SwarmEvent>? onEvent,
        SwarmExecutionContext swarmContext,
        CancellationToken ct)
    {
        onEvent?.Invoke(evt);
        await _session.RecordAsync(evt, swarmContext, ct).ConfigureAwait(false);
    }

    /// <summary>Thread-safe token counter for concurrent task execution.</summary>
    internal sealed class TokenCounter
    {
        private int _value;
        public int Value => _value;
        public void Add(int tokens) => Interlocked.Add(ref _value, tokens);
    }
}
