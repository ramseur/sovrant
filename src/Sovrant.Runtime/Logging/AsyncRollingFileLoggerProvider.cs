using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Logging;

/// <summary>
/// A lightweight, non-blocking file logger that writes log entries on a dedicated background thread.
/// Supports daily rolling via a <c>{Date}</c> token in the file path.
/// </summary>
public sealed class AsyncRollingFileLoggerProvider : ILoggerProvider, IAsyncDisposable
{
    private readonly string _pathTemplate;
    private readonly LogLevel _minimumLevel;
    private readonly bool _useJson;
    private readonly Channel<string> _channel;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cts = new();

    public AsyncRollingFileLoggerProvider(string pathTemplate, LogLevel minimumLevel, bool useJson)
    {
        _pathTemplate = pathTemplate;
        _minimumLevel = minimumLevel;
        _useJson = useJson;
        _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        _writerTask = Task.Run(() => WriteLoopAsync(_cts.Token));
    }

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(categoryName, _minimumLevel, _useJson, _channel.Writer);

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.TryComplete();
        _writerTask.GetAwaiter().GetResult();
        _cts.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _channel.Writer.TryComplete();
        await _writerTask.ConfigureAwait(false);
        _cts.Dispose();
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        string? currentPath = null;
        StreamWriter? writer = null;

        try
        {
            await foreach (var line in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                var resolvedPath = _pathTemplate.Replace("{Date}",
                    DateTime.UtcNow.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase);

                if (!string.Equals(resolvedPath, currentPath, StringComparison.Ordinal))
                {
                    if (writer is not null)
                    {
                        await writer.DisposeAsync().ConfigureAwait(false);
                    }

                    var dir = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

#pragma warning disable CA2000 // StreamWriter is disposed in the finally block and on daily roll
                    writer = new StreamWriter(resolvedPath, append: true)
                    {
                        AutoFlush = false,
                    };
#pragma warning restore CA2000
                    currentPath = resolvedPath;
                }

                await writer!.WriteLineAsync(line).ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        finally
        {
            if (writer is not null)
                await writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class FileLogger(
        string categoryName,
        LogLevel minimumLevel,
        bool useJson,
        ChannelWriter<string> writer) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            ScopeProvider.Push(state);

        [ThreadStatic]
        private static ScopeStack? t_scopeStack;

        private static ScopeStack ScopeProvider => t_scopeStack ??= new ScopeStack();

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);

            string line;
            if (useJson)
            {
                var scopeProps = CollectScopeProperties();
                line = FormatJson(timestamp, logLevel, categoryName, eventId, message, exception, scopeProps);
            }
            else
            {
                line = $"{timestamp} [{logLevel,-12}] {categoryName}: {message}";
                if (exception is not null)
                    line += Environment.NewLine + exception;
            }

            writer.TryWrite(line);
        }

        private static string FormatJson(
            string timestamp, LogLevel level, string category, EventId eventId,
            string message, Exception? exception, Dictionary<string, string>? scopeProps)
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append("{\"t\":\"").Append(timestamp)
              .Append("\",\"l\":\"").Append(level)
              .Append("\",\"c\":\"").Append(EscapeJson(category))
              .Append("\",\"m\":\"").Append(EscapeJson(message)).Append('"');

            if (eventId.Id != 0)
                sb.Append(",\"eid\":").Append(eventId.Id);

            if (scopeProps is { Count: > 0 })
            {
                foreach (var (key, value) in scopeProps)
                {
                    sb.Append(",\"").Append(EscapeJson(key)).Append("\":\"").Append(EscapeJson(value)).Append('"');
                }
            }

            if (exception is not null)
                sb.Append(",\"ex\":\"").Append(EscapeJson(exception.ToString())).Append('"');

            sb.Append('}');
            return sb.ToString();
        }

        private static Dictionary<string, string>? CollectScopeProperties()
        {
            var stack = t_scopeStack;
            if (stack is null || stack.Current is null) return null;

            Dictionary<string, string>? result = null;
            var current = stack.Current;
            while (current is not null)
            {
                if (current.State is IEnumerable<KeyValuePair<string, object?>> props)
                {
                    result ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kv in props)
                    {
                        if (kv.Value is not null)
                            result.TryAdd(kv.Key, kv.Value.ToString() ?? string.Empty);
                    }
                }
                current = current.Parent;
            }
            return result;
        }

        private static string EscapeJson(string s)
        {
            if (s.AsSpan().IndexOfAny("\"\\") < 0 && !s.Contains('\n', StringComparison.Ordinal) && !s.Contains('\r', StringComparison.Ordinal))
                return s;

            return s.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal)
                    .Replace("\n", "\\n", StringComparison.Ordinal)
                    .Replace("\r", "\\r", StringComparison.Ordinal);
        }
    }

    private sealed class ScopeStack
    {
        public ScopeEntry? Current { get; set; }

        public ScopeDisposable Push<TState>(TState state) where TState : notnull
        {
            var entry = new ScopeEntry(state, Current);
            Current = entry;
            return new ScopeDisposable(this, entry);
        }
    }

    private sealed class ScopeEntry(object state, ScopeEntry? parent)
    {
        public object State { get; } = state;
        public ScopeEntry? Parent { get; } = parent;
    }

    private sealed class ScopeDisposable(ScopeStack stack, ScopeEntry entry) : IDisposable
    {
        public void Dispose() => stack.Current = entry.Parent;
    }
}
