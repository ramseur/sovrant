using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Aggregates all governance rules (secret detection, dangerous commands, config protection)
/// and evaluates tool operations. Respects the configured <see cref="GovernanceLevel"/>.
/// </summary>
public sealed partial class GovernanceMonitor : IGovernanceMonitor, IDisposable
{
    private readonly ILiveSettings<GovernanceConfig> _liveConfig;
    private readonly IDisposable? _changedSubscription;
    private readonly IAuditStore _auditStore;
    private readonly ILogger<GovernanceMonitor> _logger;
    private RuleSet _rules;

    /// <summary>
    /// Snapshot of derived rules built from a single <see cref="GovernanceConfig"/>.
    /// Atomic-swapped on hot reload via <see cref="Volatile.Write{T}(ref T, T)"/> so
    /// in-flight <see cref="EvaluateAsync"/> calls see a consistent set of detectors.
    /// </summary>
    private sealed record RuleSet(
        GovernanceConfig Config,
        GovernanceLevel Level,
        SecretDetector SecretDetector,
        DangerousCommandDetector CommandDetector,
        ConfigProtectionRule ConfigProtection);

    // Tool names that execute shell commands
    private static readonly HashSet<string> ShellTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Bash", "PowerShell", "REPL",
    };

    // Tool names that modify files
    private static readonly HashSet<string> FileModifyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "Edit", "Write", "EditFile", "WriteFile",
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Governance BLOCK: {Rule} — {Reason} (tool={ToolName}, session={SessionId})")]
    private static partial void LogBlock(ILogger logger, string rule, string reason, string toolName, string sessionId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Governance WARN: {Rule} — {Reason} (tool={ToolName}, session={SessionId})")]
    private static partial void LogWarn(ILogger logger, string rule, string reason, string toolName, string sessionId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Governance audit: {Rule} — {Reason} (tool={ToolName})")]
    private static partial void LogAudit(ILogger logger, string rule, string reason, string toolName);

    /// <summary>
    /// DI ctor — accepts the hot-reloadable <see cref="ILiveSettings{GovernanceConfig}"/>.
    /// Tests with a static <see cref="GovernanceConfig"/> wrap it in
    /// <see cref="LiveSettings.Static{T}"/>.
    /// </summary>
    public GovernanceMonitor(
        ILiveSettings<GovernanceConfig> liveConfig,
        IAuditStore auditStore,
        ILogger<GovernanceMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(liveConfig);
        _liveConfig = liveConfig;
        _logger = logger;
        _auditStore = auditStore;
        _rules = BuildRules(liveConfig.Current);
        _changedSubscription = liveConfig.OnChanged(fresh =>
            Volatile.Write(ref _rules, BuildRules(fresh)));
    }

    /// <inheritdoc/>
    public void Dispose() => _changedSubscription?.Dispose();

    private static RuleSet BuildRules(GovernanceConfig config) => new(
        Config: config,
        Level: config.Level,
        SecretDetector: new SecretDetector(config.SecretPatterns),
        CommandDetector: new DangerousCommandDetector(config.BlockedCommands),
        ConfigProtection: new ConfigProtectionRule(config.ProtectedFiles));

    /// <inheritdoc/>
    public async Task<GovernanceVerdict> EvaluateAsync(GovernanceContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Capture the current rule snapshot once per call so a hot-reload mid-evaluation
        // doesn't tear (e.g. EvaluatePre uses one config, audit uses another).
        var rules = Volatile.Read(ref _rules);
        GovernanceVerdict verdict = context.Phase == GovernancePhase.Pre
            ? EvaluatePre(context, rules)
            : EvaluatePost(context, rules);

        // Audit logging (if enabled)
        if (rules.Config.AuditLog && verdict.Action != GovernanceAction.Allow)
        {
            await _auditStore.LogGovernanceEventAsync(context, verdict, ct).ConfigureAwait(false);
        }

        // Log bash commands (post-execution audit)
        if (rules.Config.AuditLog &&
            context.Phase == GovernancePhase.Post &&
            ShellTools.Contains(context.ToolName) &&
            !string.IsNullOrEmpty(context.ToolInput))
        {
            await _auditStore.LogBashCommandAsync(
                context.ToolInput,
                context.SessionId,
                exitCode: 0, // We don't have exit code in this context; log as 0 for successful execution
                ct).ConfigureAwait(false);
        }

        // Enforcement based on governance level
        switch (verdict.Action)
        {
            case GovernanceAction.Block:
                LogBlock(_logger, verdict.Rule, verdict.Reason, context.ToolName, context.SessionId ?? "unknown");
                if (rules.Level == GovernanceLevel.Minimal)
                {
                    // Minimal mode: downgrade block to audit-only
                    LogAudit(_logger, verdict.Rule, verdict.Reason, context.ToolName);
                    return GovernanceVerdict.Allowed;
                }
                if (rules.Level == GovernanceLevel.Standard)
                {
                    // Standard mode: downgrade block to warn
                    return verdict with { Action = GovernanceAction.Warn };
                }
                // Strict mode: keep block
                return verdict;

            case GovernanceAction.Warn:
                LogWarn(_logger, verdict.Rule, verdict.Reason, context.ToolName, context.SessionId ?? "unknown");
                if (rules.Level == GovernanceLevel.Minimal)
                {
                    LogAudit(_logger, verdict.Rule, verdict.Reason, context.ToolName);
                    return GovernanceVerdict.Allowed;
                }
                return verdict;

            default:
                return verdict;
        }
    }

    private static GovernanceVerdict EvaluatePre(GovernanceContext context, RuleSet rules)
    {
        // Check for dangerous commands in shell tools
        if (ShellTools.Contains(context.ToolName) && !string.IsNullOrEmpty(context.ToolInput))
        {
            var dangerousPattern = rules.CommandDetector.Check(context.ToolInput);
            if (dangerousPattern is not null)
                return new GovernanceVerdict(GovernanceAction.Block,
                    $"Dangerous command detected: '{dangerousPattern}'",
                    "DangerousCommand");

            // Commit quality gate: check for git commit
            if (context.ToolInput.Contains("git commit", StringComparison.OrdinalIgnoreCase))
                return new GovernanceVerdict(GovernanceAction.Warn,
                    "git commit detected — consider running /verify first",
                    "CommitQualityGate");
        }

        // Check for config file protection on file-modifying tools
        if (FileModifyTools.Contains(context.ToolName))
        {
            if (rules.ConfigProtection.IsProtected(context.FilePath))
                return new GovernanceVerdict(GovernanceAction.Block,
                    $"Protected configuration file: '{context.FilePath}'",
                    "ConfigProtection");
        }

        return GovernanceVerdict.Allowed;
    }

    private static GovernanceVerdict EvaluatePost(GovernanceContext context, RuleSet rules)
    {
        // Secret detection on tool input and output
        var textToScan = $"{context.ToolInput}\n{context.ToolOutput}";
        var secretFinding = rules.SecretDetector.Scan(textToScan);
        if (secretFinding is not null)
            return new GovernanceVerdict(GovernanceAction.Warn,
                secretFinding,
                "SecretDetection");

        return GovernanceVerdict.Allowed;
    }

}
