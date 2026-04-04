using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tools;

/// <summary>Executes tools after evaluating them against the active permission policy.</summary>
public sealed partial class DefaultToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPermissionPolicy _policy;
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

    public DefaultToolExecutor(
        IToolRegistry registry,
        IPermissionPolicy policy,
        ILogger<DefaultToolExecutor> logger)
    {
        _registry = registry;
        _policy = policy;
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
                LogConfirmationRequired(_logger, toolName);
                // In non-interactive contexts, treat RequireConfirmation as Deny.
                // Interactive confirmation is handled by the CLI layer.
                return new ToolExecutionResult(false,
                    $"Tool '{toolName}' requires user confirmation.", IsError: true);
        }

        if (!_registry.TryGetHandler(toolName, out var handler) || handler is null)
        {
            LogNotFound(_logger, toolName);
            return new ToolExecutionResult(false,
                $"Unknown tool: '{toolName}'.", IsError: true);
        }

        LogExecuting(_logger, toolName);
        var sw = Stopwatch.StartNew();
        try
        {
            var output = await handler(input, ct).ConfigureAwait(false);

            // Large results (> 50 KB) are offloaded to a temp file.
            if (output.Length > 50 * 1024)
                output = await OffloadToTempFileAsync(toolName, output, ct).ConfigureAwait(false);

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
    }

    private static async Task<string> OffloadToTempFileAsync(
        string toolName,
        string content,
        CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"sovrant_{toolName}_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempPath, content, ct).ConfigureAwait(false);
        return $"[Large output ({content.Length:N0} bytes) written to: {tempPath}]";
    }
}
