using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Aggregates all governance rules (secret detection, dangerous commands, config protection)
/// and evaluates tool operations. Respects the configured <see cref="GovernanceLevel"/>.
/// </summary>
public sealed partial class GovernanceMonitor : IGovernanceMonitor, IDisposable
{
    private readonly GovernanceConfig _config;
    private readonly GovernanceLevel _level;
    private readonly SecretDetector _secretDetector;
    private readonly DangerousCommandDetector _commandDetector;
    private readonly ConfigProtectionRule _configProtection;
    private readonly AuditLogger _auditLogger;
    private readonly ILogger<GovernanceMonitor> _logger;

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

    public GovernanceMonitor(GovernanceConfig config, ILogger<GovernanceMonitor> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _level = config.Level;
        _logger = logger;
        _secretDetector = new SecretDetector(config.SecretPatterns);
        _commandDetector = new DangerousCommandDetector(config.BlockedCommands);
        _configProtection = new ConfigProtectionRule(config.ProtectedFiles);
        _auditLogger = new AuditLogger();
    }

    /// <inheritdoc/>
    public async Task<GovernanceVerdict> EvaluateAsync(GovernanceContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        GovernanceVerdict verdict;

        if (context.Phase == GovernancePhase.Pre)
            verdict = EvaluatePre(context);
        else
            verdict = EvaluatePost(context);

        // Audit logging (if enabled)
        if (_config.AuditLog && verdict.Action != GovernanceAction.Allow)
        {
            await _auditLogger.LogGovernanceEventAsync(context, verdict, ct).ConfigureAwait(false);
        }

        // Log bash commands (post-execution audit)
        if (_config.AuditLog &&
            context.Phase == GovernancePhase.Post &&
            ShellTools.Contains(context.ToolName) &&
            !string.IsNullOrEmpty(context.ToolInput))
        {
            await _auditLogger.LogBashCommandAsync(
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
                if (_level == GovernanceLevel.Minimal)
                {
                    // Minimal mode: downgrade block to audit-only
                    LogAudit(_logger, verdict.Rule, verdict.Reason, context.ToolName);
                    return GovernanceVerdict.Allowed;
                }
                if (_level == GovernanceLevel.Standard)
                {
                    // Standard mode: downgrade block to warn
                    return verdict with { Action = GovernanceAction.Warn };
                }
                // Strict mode: keep block
                return verdict;

            case GovernanceAction.Warn:
                LogWarn(_logger, verdict.Rule, verdict.Reason, context.ToolName, context.SessionId ?? "unknown");
                if (_level == GovernanceLevel.Minimal)
                {
                    LogAudit(_logger, verdict.Rule, verdict.Reason, context.ToolName);
                    return GovernanceVerdict.Allowed;
                }
                return verdict;

            default:
                return verdict;
        }
    }

    private GovernanceVerdict EvaluatePre(GovernanceContext context)
    {
        // Check for dangerous commands in shell tools
        if (ShellTools.Contains(context.ToolName) && !string.IsNullOrEmpty(context.ToolInput))
        {
            var dangerousPattern = _commandDetector.Check(context.ToolInput);
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
            if (_configProtection.IsProtected(context.FilePath))
                return new GovernanceVerdict(GovernanceAction.Block,
                    $"Protected configuration file: '{context.FilePath}'",
                    "ConfigProtection");
        }

        return GovernanceVerdict.Allowed;
    }

    private GovernanceVerdict EvaluatePost(GovernanceContext context)
    {
        // Secret detection on tool input and output
        var textToScan = $"{context.ToolInput}\n{context.ToolOutput}";
        var secretFinding = _secretDetector.Scan(textToScan);
        if (secretFinding is not null)
            return new GovernanceVerdict(GovernanceAction.Warn,
                secretFinding,
                "SecretDetection");

        return GovernanceVerdict.Allowed;
    }

    public void Dispose() => _auditLogger.Dispose();
}
