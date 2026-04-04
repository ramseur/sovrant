using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Sovrant.Runtime.Logging;

/// <summary>
/// Extension methods to configure Sovrant's structured async logging pipeline.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Configures the logging pipeline from <c>SOVRANT_LOG_*</c> environment variables.
    /// Adds console (optional) and async rolling file sinks.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="consoleMinOverride">
    /// Override the minimum level for the console sink only. Useful for the CLI where console
    /// noise should be suppressed unless the user explicitly sets <c>SOVRANT_LOG_LEVEL</c>.
    /// When <c>null</c>, uses the same level as the global minimum.
    /// </param>
    public static ILoggingBuilder AddSovrantLogging(
        this ILoggingBuilder builder,
        LogLevel? consoleMinOverride = null)
    {
        var config = SovrantLogConfig.FromEnvironment();

        builder.ClearProviders();
        builder.SetMinimumLevel(config.MinimumLevel);

        if (config.ConsoleEnabled)
        {
            if (string.Equals(config.Format, "json", StringComparison.OrdinalIgnoreCase))
            {
                builder.AddJsonConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                });
            }
            else
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
                    options.SingleLine = true;
                });
            }

            // Apply console-specific minimum level filter if provided.
            if (consoleMinOverride is not null)
            {
                builder.AddFilter<ConsoleLoggerProvider>(null, consoleMinOverride.Value);
            }
        }

        // Async rolling file sink — AddProvider takes ownership of the provider's lifetime.
#pragma warning disable CA2000 // Dispose objects before losing scope — ILoggingBuilder.AddProvider takes ownership
        if (!string.IsNullOrEmpty(config.FilePath))
        {
            var useJson = string.Equals(config.Format, "json", StringComparison.OrdinalIgnoreCase);
            builder.AddProvider(new AsyncRollingFileLoggerProvider(config.FilePath, config.MinimumLevel, useJson));
        }
#pragma warning restore CA2000

        return builder;
    }
}
