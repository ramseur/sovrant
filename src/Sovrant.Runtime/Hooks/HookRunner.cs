using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Hooks;

/// <summary>
/// Loads hook definitions from <see cref="IHookStore"/> and fires matching
/// hooks at agent lifecycle events.
/// </summary>
/// <remarks>
/// Profile is controlled by <c>SOVRANT_HOOK_PROFILE</c> (minimal / standard / strict; default: standard).
/// Individual event types can be suppressed via <c>SOVRANT_DISABLED_HOOKS</c> (comma-separated event names).
/// </remarks>
public sealed partial class HookRunner : IHookRunner
{
    internal enum HookProfile { Minimal, Standard, Strict }

    private volatile IReadOnlyList<HookConfig> _hooks;
    private readonly IHookStore? _store;
    private readonly HookProfile _profile;
    private readonly IReadOnlySet<string> _disabled;
    private readonly ILogger<HookRunner> _logger;
    private readonly Func<string, int, CancellationToken, Task<int>> _processRunner;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load hooks from store: {Error}")]
    private static partial void LogLoadError(ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hook fired: event={Event}, command={Command}")]
    private static partial void LogHookFired(ILogger logger, HookEvent @event, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PreToolUse hook blocked tool: command={Command}, exitCode={ExitCode}")]
    private static partial void LogHookBlocked(ILogger logger, string command, int exitCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hook timed out after {TimeoutMs} ms: command={Command}")]
    private static partial void LogTimeout(ILogger logger, int timeoutMs, string command);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hook execution error: command={Command}, error={Error}")]
    private static partial void LogHookError(ILogger logger, string command, string error);

    /// <summary>Production constructor — loads enabled hooks from <see cref="IHookStore"/>.</summary>
    public HookRunner(IHookStore store, ILogger<HookRunner> logger)
        : this(LoadHooksFromStore(store, logger), ResolveProfile(), ResolveDisabled(), logger, null)
    {
        _store = store;
    }

    /// <summary>Testable constructor — accepts pre-built config and an optional mock process runner.</summary>
    internal HookRunner(
        IReadOnlyList<HookConfig> hooks,
        HookProfile profile,
        IReadOnlySet<string> disabled,
        ILogger<HookRunner> logger,
        Func<string, int, CancellationToken, Task<int>>? processRunner)
    {
        _hooks = hooks;
        _profile = profile;
        _disabled = disabled;
        _logger = logger;
        _processRunner = processRunner ?? ExecuteProcessAsync;
    }

    /// <summary>
    /// Reloads enabled hooks from the store. Called by the Web/Desktop UI after
    /// hooks are added, edited, or deleted so the running engine picks up the
    /// change without a restart.
    /// </summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (_store is null) return;
        try
        {
            _hooks = await _store.GetEnabledAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            LogLoadError(_logger, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RunAsync(HookEvent hookEvent, HookContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsAllowedByProfile(hookEvent)) return true;
        if (_disabled.Contains(hookEvent.ToString())) return true;

        var matching = _hooks
            .Where(h => h.Event == hookEvent && MatchesContext(h, context))
            .ToList();

        if (matching.Count == 0) return true;

        var isBlocking = hookEvent == HookEvent.PreToolUse;

        foreach (var hook in matching)
        {
            var command = SubstituteTemplates(hook.Command, context);
            LogHookFired(_logger, hookEvent, command);

            try
            {
                var exitCode = await _processRunner(command, hook.TimeoutMs, ct).ConfigureAwait(false);
                if (isBlocking && exitCode != 0)
                {
                    LogHookBlocked(_logger, command, exitCode);
                    return false;
                }
            }
            catch (OperationCanceledException) when (isBlocking && ct.IsCancellationRequested)
            {
                throw;  // propagate only if the caller's own token was cancelled
            }
            catch (OperationCanceledException)
            {
                // Timeout or non-blocking hook cancellation — block the tool for PreToolUse,
                // silently swallow for all other events.
                if (isBlocking) return false;
            }
            catch (InvalidOperationException ex)
            {
                LogHookError(_logger, command, ex.Message);
                if (isBlocking) return false;
            }
            catch (IOException ex)
            {
                LogHookError(_logger, command, ex.Message);
                if (isBlocking) return false;
            }
        }

        return true;
    }

    private bool IsAllowedByProfile(HookEvent hookEvent) => _profile switch
    {
        HookProfile.Minimal => hookEvent is HookEvent.SessionStart or HookEvent.SessionEnd,
        HookProfile.Standard => hookEvent is not HookEvent.PreToolUse,
        HookProfile.Strict => true,
        _ => true,
    };

    private static bool MatchesContext(HookConfig hook, HookContext context)
    {
        if (hook.Matcher is null) return true;

        if (hook.Matcher.Tool is not null &&
            !string.Equals(hook.Matcher.Tool, context.ToolName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (hook.Matcher.Glob is not null)
        {
            // If no file path available, or the path doesn't match the glob, skip this hook.
            if (context.FilePath is null || !GlobMatches(hook.Matcher.Glob, context.FilePath))
                return false;
        }

        return true;
    }

    private static bool GlobMatches(string pattern, string path)
    {
        var fileName = Path.GetFileName(path);
        return WildcardMatch(pattern, fileName) || WildcardMatch(pattern, path);
    }

    private static bool WildcardMatch(string pattern, string input)
    {
        // Convert glob to regex: ** → .*, * → [^/\\]*, ? → .
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", @"[^/\\]*", StringComparison.Ordinal)
            .Replace(@"\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    private static string SubstituteTemplates(string command, HookContext ctx) =>
        command
            .Replace("{session_id}", ctx.SessionId, StringComparison.Ordinal)
            .Replace("{tool}", ctx.ToolName ?? string.Empty, StringComparison.Ordinal)
            .Replace("{file}", ctx.FilePath ?? string.Empty, StringComparison.Ordinal)
            .Replace("{output}", ctx.ToolOutput ?? string.Empty, StringComparison.Ordinal)
            .Replace("{input}", ctx.ToolInput ?? string.Empty, StringComparison.Ordinal)
            .Replace("{is_error}", ctx.IsError ? "true" : "false", StringComparison.Ordinal)
            .Replace("{stop_reason}", ctx.StopReason ?? string.Empty, StringComparison.Ordinal)
            .Replace("{tokens}", ctx.InputTokens.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private async Task<int> ExecuteProcessAsync(string command, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);

            var (fileName, cmdArg) = OperatingSystem.IsWindows()
                ? ("cmd.exe", "/C")
                : ("/bin/sh", "-c");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
            };
            psi.ArgumentList.Add(cmdArg);
            psi.ArgumentList.Add(command);

            using var process = new Process { StartInfo = psi };
            process.Start();
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                    try { process.Kill(); } catch (InvalidOperationException) { }
                throw;
            }
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogTimeout(_logger, timeoutMs, command);
            return -1;
        }
    }

    private static IReadOnlyList<HookConfig> LoadHooksFromStore(IHookStore store, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            // Sync at construction is acceptable: hooks are config, loaded once,
            // and the SQLite read is a single indexed scan over a small table.
            return store.GetEnabledAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            LogLoadError(logger, ex.Message);
            return [];
        }
    }

    internal static HookProfile ResolveProfile() =>
        (Environment.GetEnvironmentVariable("SOVRANT_HOOK_PROFILE") ?? string.Empty)
            .Trim().ToUpperInvariant() switch
        {
            "MINIMAL" => HookProfile.Minimal,
            "STRICT" => HookProfile.Strict,
            _ => HookProfile.Standard,
        };

    internal static IReadOnlySet<string> ResolveDisabled()
    {
        var raw = Environment.GetEnvironmentVariable("SOVRANT_DISABLED_HOOKS");
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
