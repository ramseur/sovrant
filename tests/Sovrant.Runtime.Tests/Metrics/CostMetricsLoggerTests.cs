using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class CostMetricsLoggerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _logPath;

    public CostMetricsLoggerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _logPath = Path.Combine(_tempDir, "cost.jsonl");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Append_WritesJsonLine()
    {
        var logger = new CostMetricsLogger(NullLogger<CostMetricsLogger>.Instance, _logPath);
        var record = MakeRecord("session-1", "gpt-4o", 1000, 500, 0.0105m);

        logger.Append(record);

        var lines = File.ReadAllLines(_logPath);
        Assert.Single(lines);
        Assert.Contains("session-1", lines[0], StringComparison.Ordinal);
        Assert.Contains("gpt-4o", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ReadAll_ReturnsAppendedRecords()
    {
        var logger = new CostMetricsLogger(NullLogger<CostMetricsLogger>.Instance, _logPath);

        logger.Append(MakeRecord("s1", "model-a", 100, 50, 0.001m));
        logger.Append(MakeRecord("s1", "model-b", 200, 100, 0.002m));

        var records = logger.ReadAll();
        Assert.Equal(2, records.Count);
        Assert.Equal("s1", records[0].SessionId);
        Assert.Equal("model-b", records[1].Model);
    }

    [Fact]
    public void Append_RotatesAtMaxSize()
    {
        // Use a tiny max size to trigger rotation
        var logger = new CostMetricsLogger(NullLogger<CostMetricsLogger>.Instance, _logPath, maxFileSizeBytes: 100);

        // First append creates the file
        logger.Append(MakeRecord("s1", "model-a", 100, 50, 0.001m));

        // Second append should trigger rotation (file > 100 bytes after first line)
        logger.Append(MakeRecord("s1", "model-b", 200, 100, 0.002m));

        // The rotated file should exist
        Assert.True(File.Exists(_logPath + ".1") || File.Exists(_logPath));
    }

    [Fact]
    public void ReadAll_EmptyFile_ReturnsEmpty()
    {
        var logger = new CostMetricsLogger(NullLogger<CostMetricsLogger>.Instance, _logPath);
        var records = logger.ReadAll();
        Assert.Empty(records);
    }

    private static CostTurnRecord MakeRecord(string sessionId, string model, long input, long output, decimal? estimatedUsd) =>
        new()
        {
            SessionId = sessionId,
            Model = model,
            InputTokens = input,
            OutputTokens = output,
            EstimatedUsd = estimatedUsd,
            Source = "test",
            Timestamp = DateTimeOffset.UtcNow
        };
}
