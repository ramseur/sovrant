using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Sovrant.Runtime.Metrics;

/// <summary>
/// Append-only JSONL writer for per-turn cost records.
/// Writes to <c>~/.sovrant/metrics/cost.jsonl</c> by default.
/// Rotates at 10 MB to <c>cost.jsonl.1</c>.
/// </summary>
public sealed partial class CostMetricsLogger
{
    private readonly string _path;
    private readonly long _maxFileSizeBytes;
    private readonly object _writeLock = new();
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to write cost metrics to {Path}")]
    private static partial void LogWriteFailed(ILogger logger, Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rotated cost metrics log to {Path}")]
    private static partial void LogRotated(ILogger logger, string path);

    public CostMetricsLogger(
        ILogger<CostMetricsLogger> logger,
        string? path = null,
        long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        _logger = logger;
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "metrics", "cost.jsonl");
        _maxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>The path to the current JSONL log file.</summary>
    public string FilePath => _path;

    /// <summary>
    /// Appends a single cost record as one JSON line.
    /// </summary>
    public void Append(CostTurnRecord record)
    {
        try
        {
            var line = JsonSerializer.Serialize(record, s_jsonOptions);

            lock (_writeLock)
            {
                var dir = System.IO.Path.GetDirectoryName(_path);
                if (dir is not null)
                    Directory.CreateDirectory(dir);

                RotateIfNeeded();
                File.AppendAllText(_path, line + "\n");
            }
        }
        catch (Exception ex) when (ex is IOException)
        {
            LogWriteFailed(_logger, ex, _path);
        }
    }

    /// <summary>
    /// Reads all records from the current log file.
    /// </summary>
    public IReadOnlyList<CostTurnRecord> ReadAll()
    {
        if (!File.Exists(_path))
            return [];

        var records = new List<CostTurnRecord>();
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var record = JsonSerializer.Deserialize<CostTurnRecord>(line, s_jsonOptions);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_path)) return;

        var info = new FileInfo(_path);
        if (info.Length < _maxFileSizeBytes) return;

        var rotatedPath = _path + ".1";
        if (File.Exists(rotatedPath))
            File.Delete(rotatedPath);

        File.Move(_path, rotatedPath);
        LogRotated(_logger, rotatedPath);
    }
}
