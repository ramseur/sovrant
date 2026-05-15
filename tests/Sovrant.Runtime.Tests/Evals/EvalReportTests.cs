using Sovrant.Runtime.Evals;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class EvalReportTests
{
    [Fact]
    public void PassRate_AllPassed()
    {
        var report = CreateReport(
            MakeResult("a", passed: true),
            MakeResult("b", passed: true));

        Assert.Equal(1.0, report.PassRate);
        Assert.Equal(2, report.TotalPassed);
        Assert.Equal(0, report.TotalFailed);
    }

    [Fact]
    public void PassRate_MixedResults()
    {
        var report = CreateReport(
            MakeResult("a", passed: true),
            MakeResult("b", passed: false));

        Assert.Equal(0.5, report.PassRate);
        Assert.Equal(1, report.TotalPassed);
        Assert.Equal(1, report.TotalFailed);
    }

    [Fact]
    public void PassRate_WithSkipped_ExcludesSkipped()
    {
        var report = CreateReport(
            MakeResult("a", passed: true),
            MakeResult("b", passed: false, skipped: true));

        Assert.Equal(1.0, report.PassRate);
        Assert.Equal(1, report.TotalPassed);
        Assert.Equal(0, report.TotalFailed);
        Assert.Equal(1, report.TotalSkipped);
    }

    [Fact]
    public void PassRate_Empty_ReturnsZero()
    {
        var report = CreateReport();
        Assert.Equal(0.0, report.PassRate);
    }

    [Fact]
    public void PassAt1Rate_Computed_Correctly()
    {
        var r1 = new EvalResult
        {
            EvalName = "a",
            Attempts = [new GradeResult(true, "ok")],
            Passed = true,
            GraderType = GraderType.Code,
        };
        var r2 = new EvalResult
        {
            EvalName = "b",
            Attempts = [new GradeResult(false, "fail"), new GradeResult(true, "ok")],
            Passed = true, // passed at k but not at 1
            GraderType = GraderType.Code,
        };

        var report = CreateReport(r1, r2);
        Assert.Equal(0.5, report.PassAt1Rate);
    }

    [Fact]
    public void EvalResult_PassCount_Correct()
    {
        var result = new EvalResult
        {
            EvalName = "test",
            Attempts = [
                new GradeResult(true, "ok"),
                new GradeResult(false, "fail"),
                new GradeResult(true, "ok"),
            ],
            Passed = true,
            GraderType = GraderType.Code,
        };

        Assert.Equal(2, result.PassCount);
        Assert.True(result.PassAt1);
    }

    [Fact]
    public void EvalResult_AverageScore_Computed()
    {
        var result = new EvalResult
        {
            EvalName = "test",
            Attempts = [
                new GradeResult(true, "ok", Score: 8.0),
                new GradeResult(true, "ok", Score: 6.0),
            ],
            Passed = true,
            GraderType = GraderType.Model,
        };

        Assert.Equal(7.0, result.AverageScore);
    }

    [Fact]
    public void EvalResult_AverageScore_Null_WhenNoScores()
    {
        var result = new EvalResult
        {
            EvalName = "test",
            Attempts = [new GradeResult(true, "ok")],
            Passed = true,
            GraderType = GraderType.Code,
        };

        Assert.Null(result.AverageScore);
    }

    [Fact]
    public void EvalResult_TotalDuration_Summed()
    {
        var result = new EvalResult
        {
            EvalName = "test",
            Attempts = [
                new GradeResult(true, "ok", Duration: TimeSpan.FromSeconds(1)),
                new GradeResult(true, "ok", Duration: TimeSpan.FromSeconds(2)),
            ],
            Passed = true,
            GraderType = GraderType.Code,
        };

        Assert.Equal(3.0, result.TotalDuration.TotalSeconds);
    }

    [Fact]
    public void ToReport_ContainsExpectedSections()
    {
        var report = CreateReport(
            MakeResult("eval-a", passed: true),
            MakeResult("eval-b", passed: false));

        var text = report.ToReport();

        Assert.Contains("Eval Report: test-suite", text);
        Assert.Contains("eval-a", text);
        Assert.Contains("eval-b", text);
        Assert.Contains("PASS", text);
        Assert.Contains("FAIL", text);
        Assert.Contains("pass@1", text);
        Assert.Contains("pass@k", text);
    }

    [Fact]
    public void GradeResult_Defaults()
    {
        var grade = new GradeResult(true, "output");
        Assert.True(grade.Passed);
        Assert.Equal("output", grade.Output);
        Assert.Null(grade.Score);
        Assert.Equal(TimeSpan.Zero, grade.Duration);
    }

    private static EvalReport CreateReport(params EvalResult[] results) => new()
    {
        SuiteName = "test-suite",
        Results = results,
        StartedAt = DateTimeOffset.UtcNow,
        Duration = TimeSpan.FromSeconds(1),
    };

    private static EvalResult MakeResult(string name, bool passed, bool skipped = false) => new()
    {
        EvalName = name,
        Attempts = passed ? [new GradeResult(true, "ok")] : [new GradeResult(false, "fail")],
        Passed = passed,
        Skipped = skipped,
        GraderType = GraderType.Code,
    };
}
