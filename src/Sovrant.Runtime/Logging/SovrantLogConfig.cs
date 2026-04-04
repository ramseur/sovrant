using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Logging;

/// <summary>
/// Reads the <c>SOVRANT_LOG_*</c> environment variables and exposes them as typed properties.
/// </summary>
public sealed record SovrantLogConfig
{
    /// <summary>Minimum log level. Default: <see cref="LogLevel.Information"/>.</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;

    /// <summary>
    /// Rolling file path pattern. The token <c>{Date}</c> is replaced with <c>yyyyMMdd</c>.
    /// Set to empty string to disable file logging.
    /// Default: <c>~/.sovrant/logs/sovrant-{Date}.log</c>.
    /// </summary>
    public string FilePath { get; init; } = DefaultFilePath();

    /// <summary>Whether to write logs to the console. Default: <c>true</c>.</summary>
    public bool ConsoleEnabled { get; init; } = true;

    /// <summary>Output format: <c>"text"</c> (human-readable) or <c>"json"</c> (structured). Default: <c>"text"</c>.</summary>
    public string Format { get; init; } = "text";

    /// <summary>Reads configuration from <c>SOVRANT_LOG_*</c> environment variables.</summary>
    public static SovrantLogConfig FromEnvironment()
    {
        var levelStr = Environment.GetEnvironmentVariable("SOVRANT_LOG_LEVEL");
        var level = LogLevel.Information;
        if (!string.IsNullOrEmpty(levelStr) && Enum.TryParse<LogLevel>(levelStr, ignoreCase: true, out var parsed))
            level = parsed;

        var filePath = Environment.GetEnvironmentVariable("SOVRANT_LOG_FILE") ?? DefaultFilePath();

        var consoleStr = Environment.GetEnvironmentVariable("SOVRANT_LOG_CONSOLE");
        var console = !string.Equals(consoleStr, "false", StringComparison.OrdinalIgnoreCase);

        var format = Environment.GetEnvironmentVariable("SOVRANT_LOG_FORMAT") ?? "text";

        return new SovrantLogConfig
        {
            MinimumLevel = level,
            FilePath = filePath,
            ConsoleEnabled = console,
            Format = format,
        };
    }

    private static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "logs", "sovrant-{Date}.log");
}
