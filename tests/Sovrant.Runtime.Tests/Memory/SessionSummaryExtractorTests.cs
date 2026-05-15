using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class SessionSummaryExtractorTests
{
    [Fact]
    public void Extract_EmptyEntries_ReturnsUnknownOutcome()
    {
        var summary = SessionSummaryExtractor.Extract("sess-1", "/project", []);

        Assert.Equal("sess-1", summary.SessionId);
        Assert.Equal("/project", summary.Project);
        Assert.Equal(SessionOutcome.Unknown, summary.Outcome);
        Assert.Equal(0, summary.TurnCount);
    }

    [Fact]
    public void Extract_SingleUserMessage_CountsTurn()
    {
        var entries = new[]
        {
            MakeEntry("user", "Fix the login bug", inputTokens: 50),
        };

        var summary = SessionSummaryExtractor.Extract("sess-2", "/proj", entries);

        Assert.Equal(1, summary.TurnCount);
        Assert.Single(summary.Tasks);
        Assert.Equal("Fix the login bug", summary.Tasks[0]);
        Assert.Equal(50, summary.TotalInputTokens);
    }

    [Fact]
    public void Extract_TracksToolsUsed()
    {
        var entries = new[]
        {
            MakeEntry("user", "List files"),
            MakeEntry("assistant", "I'll list files", toolName: "glob"),
            MakeEntry("tool_result", "file1.txt\nfile2.txt", toolName: "glob"),
            MakeEntry("assistant", "Here are the files."),
        };

        var summary = SessionSummaryExtractor.Extract("sess-3", "/proj", entries);

        Assert.Contains("glob", summary.ToolsUsed);
        Assert.Equal(SessionOutcome.Success, summary.Outcome);
    }

    [Fact]
    public void Extract_ToolError_SetsFailureOutcome()
    {
        var entries = new[]
        {
            MakeEntry("user", "Delete everything"),
            MakeEntry("assistant", "Running delete", toolName: "bash"),
            MakeEntry("tool_result", "Permission denied", toolName: "bash", isError: true),
        };

        var summary = SessionSummaryExtractor.Extract("sess-4", "/proj", entries);

        Assert.Equal(SessionOutcome.Failure, summary.Outcome);
        Assert.Equal(1, summary.ErrorCount);
    }

    [Fact]
    public void Extract_SuccessAfterError_SetsSuccess()
    {
        var entries = new[]
        {
            MakeEntry("user", "Fix the test"),
            MakeEntry("assistant", "Running test", toolName: "bash"),
            MakeEntry("tool_result", "FAIL", toolName: "bash", isError: true),
            MakeEntry("user", "Try again"),
            MakeEntry("assistant", "Running test", toolName: "bash"),
            MakeEntry("tool_result", "PASS", toolName: "bash"),
        };

        var summary = SessionSummaryExtractor.Extract("sess-5", "/proj", entries);

        Assert.Equal(SessionOutcome.Success, summary.Outcome);
        Assert.Equal(1, summary.ErrorCount);
        Assert.Equal(2, summary.TurnCount);
    }

    [Fact]
    public void Extract_AccumulatesTokens()
    {
        var entries = new[]
        {
            MakeEntry("user", "Hello", inputTokens: 100),
            MakeEntry("assistant", "Hi there", outputTokens: 50),
            MakeEntry("user", "Bye", inputTokens: 80),
            MakeEntry("assistant", "Goodbye", outputTokens: 30),
        };

        var summary = SessionSummaryExtractor.Extract("sess-6", "/proj", entries);

        Assert.Equal(180, summary.TotalInputTokens);
        Assert.Equal(80, summary.TotalOutputTokens);
    }

    [Fact]
    public void Extract_TruncatesLongUserMessages()
    {
        var longMessage = new string('A', 300);
        var entries = new[] { MakeEntry("user", longMessage) };

        var summary = SessionSummaryExtractor.Extract("sess-7", "/proj", entries);

        Assert.True(summary.Tasks[0].Length <= 204); // 200 + "..."
        Assert.EndsWith("...", summary.Tasks[0]);
    }

    [Fact]
    public void Extract_SetsTimestamps()
    {
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t2 = DateTimeOffset.UtcNow;

        var entries = new[]
        {
            new SessionEntry("1", t1, "user", "Start"),
            new SessionEntry("2", t2, "assistant", "Done"),
        };

        var summary = SessionSummaryExtractor.Extract("sess-8", "/proj", entries);

        Assert.Equal(t1, summary.StartedAt);
        Assert.Equal(t2, summary.EndedAt);
    }

    private static SessionEntry MakeEntry(
        string role,
        string content,
        string? toolName = null,
        bool isError = false,
        int inputTokens = 0,
        int outputTokens = 0)
    {
        return new SessionEntry(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, role, content)
        {
            ToolName = toolName,
            IsError = isError,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
        };
    }
}
