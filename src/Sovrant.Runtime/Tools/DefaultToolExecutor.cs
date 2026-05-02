using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tools;

/// <summary>Executes tools after evaluating them against the active permission policy and governance rules.</summary>
public sealed partial class DefaultToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPermissionPolicy _policy;
    private readonly IGovernanceMonitor _governance;
    private readonly IToolConfirmationHandler _confirmationHandler;
    private readonly IPerTurnApprovalCache? _approvalCache;
    private readonly ILogger<DefaultToolExecutor> _logger;

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing tool '{ToolName}'")]
    private static partial void LogExecuting(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool '{ToolName}' denied by permission policy")]
    private static partial void LogDenied(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool '{ToolName}' requires user confirmation")]
    private static partial void LogConfirmationRequired(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool '{ToolName}' not found in registry")]
    private static partial void LogNotFound(ILogger logger, string toolName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tool '{ToolName}' threw an exception")]
    private static partial void LogToolException(ILogger logger, string toolName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tool '{ToolName}' completed in {DurationMs}ms (is_error={IsError})")]
    private static partial void LogExecutionComplete(ILogger logger, string toolName, long durationMs, bool isError);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool '{ToolName}' blocked by governance: {Reason}")]
    private static partial void LogGovernanceBlocked(ILogger logger, string toolName, string reason);

    public DefaultToolExecutor(
        IToolRegistry registry,
        IPermissionPolicy policy,
        IGovernanceMonitor governance,
        IToolConfirmationHandler confirmationHandler,
        ILogger<DefaultToolExecutor> logger,
        IPerTurnApprovalCache? approvalCache = null)
    {
        _registry = registry;
        _policy = policy;
        _governance = governance;
        _confirmationHandler = confirmationHandler;
        _approvalCache = approvalCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement input,
        CancellationToken ct = default)
    {
        var isDestructive = ModeAwarePermissionPolicy.IsDestructive(toolName);
        var decision = _policy.Evaluate(toolName, isDestructive);

        switch (decision)
        {
            case PolicyDecision.Deny:
                LogDenied(_logger, toolName);
                return new ToolExecutionResult(false,
                    $"Tool '{toolName}' is blocked in the current permission mode.", IsError: true);

            case PolicyDecision.RequireConfirmation:
                if (_approvalCache is not null && _approvalCache.IsAllowed(toolName))
                    break; // user already said "always allow this turn" for this tool
                LogConfirmationRequired(_logger, toolName);
                var userDecision = await _confirmationHandler
                    .RequestConfirmationAsync(toolName, input, ct).ConfigureAwait(false);
                if (userDecision == ConfirmationDecision.Deny)
                {
                    LogDenied(_logger, toolName);
                    return new ToolExecutionResult(false,
                        $"Tool '{toolName}' was denied by the user.", IsError: true);
                }
                if (userDecision == ConfirmationDecision.AllowForTurn)
                    _approvalCache?.Allow(toolName);
                break;
        }

        if (!_registry.TryGetHandler(toolName, out var handler) || handler is null)
        {
            LogNotFound(_logger, toolName);
            return new ToolExecutionResult(false,
                $"Unknown tool: '{toolName}'.", IsError: true);
        }

        // Pre-execution governance check
        var inputText = input.ValueKind == System.Text.Json.JsonValueKind.Undefined
            ? null
            : input.ToString();
        var filePath = input.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                       input.TryGetProperty("file_path", out var fp) ? fp.GetString() :
                       input.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                       input.TryGetProperty("path", out var p) ? p.GetString() : null;

        var preContext = new GovernanceContext(GovernancePhase.Pre, toolName, inputText, FilePath: filePath);
        var preVerdict = await _governance.EvaluateAsync(preContext, ct).ConfigureAwait(false);
        if (preVerdict.Action == GovernanceAction.Block)
        {
            LogGovernanceBlocked(_logger, toolName, preVerdict.Reason);
            return new ToolExecutionResult(false,
                $"Blocked by governance ({preVerdict.Rule}): {preVerdict.Reason}", IsError: true);
        }

        LogExecuting(_logger, toolName);
        var sw = Stopwatch.StartNew();
        try
        {
            var output = await handler(input, ct).ConfigureAwait(false);

            // Post-execution governance check
            var postContext = new GovernanceContext(GovernancePhase.Post, toolName, inputText, output);
            var postVerdict = await _governance.EvaluateAsync(postContext, ct).ConfigureAwait(false);
            if (postVerdict.Action == GovernanceAction.Warn)
                output = $"[Governance warning — {postVerdict.Rule}: {postVerdict.Reason}]\n{output}";

            // Large results (> 50 KB) are offloaded to a temp file.
            if (output.Length > 50 * 1024)
                output = await OffloadToTempFileAsync(toolName, output, ct).ConfigureAwait(false);

            // Pre-execution warning appended after output
            if (preVerdict.Action == GovernanceAction.Warn)
                output = $"[Governance warning — {preVerdict.Rule}: {preVerdict.Reason}]\n{output}";

            sw.Stop();
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, false);
            return new ToolExecutionResult(true, output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false, ex.Message, IsError: true);
        }
        catch (IOException ex)
        {
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false, ex.Message, IsError: true);
        }
        catch (InvalidDataException ex)
        {
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false, ex.Message, IsError: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false, $"Access denied: {ex.Message}", IsError: true);
        }
        catch (DbException ex)
        {
            // Infrastructure failure — not something the LLM can retry its way out of.
            // The INTERNAL_ERROR prefix lets the turn loop detect and abort after one repeat.
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false,
                $"[INTERNAL_ERROR] {ex.GetType().Name}: {ex.Message}. " +
                "This is an internal system error; do not retry this tool with the same inputs. " +
                "Report the failure to the user.",
                IsError: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Catch-all so that unexpected tool failures (Win32Exception, etc.)
            // never crash the REPL — they are returned as error results to the LLM.
            sw.Stop();
            LogToolException(_logger, toolName, ex);
            LogExecutionComplete(_logger, toolName, sw.ElapsedMilliseconds, true);
            return new ToolExecutionResult(false, $"{ex.GetType().Name}: {ex.Message}", IsError: true);
        }
    }

    private static async Task<string> OffloadToTempFileAsync(
        string toolName,
        string content,
        CancellationToken ct)
    {
        // Sanitize tool name to prevent path traversal in temp file names.
        var safeName = SanitizeToolName(toolName);
        var tempPath = Path.Combine(Path.GetTempPath(), $"sovrant_{safeName}_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, content, ct).ConfigureAwait(false);
        return $"[Large output ({content.Length:N0} bytes) written to: {tempPath}]";
    }

    private static string SanitizeToolName(string name)
    {
        // Replace any character that isn't alphanumeric, hyphen, or underscore.
        var span = name.AsSpan();
        Span<char> buffer = stackalloc char[Math.Min(span.Length, 64)];
        var len = 0;
        for (var i = 0; i < span.Length && len < buffer.Length; i++)
        {
            var c = span[i];
            buffer[len++] = char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_';
        }
        return new string(buffer[..len]);
    }
}
