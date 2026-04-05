using Sovrant.Tools.Quality;

namespace Sovrant.Tools.Tests.Quality;

public sealed class VerificationResultTests
{
    [Fact]
    public void AllPassed_AllPass_ReturnsTrue()
    {
        var result = new VerificationResult { ProjectType = "Dotnet" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Build, true, "ok"));
        result.Phases.Add(new PhaseResult(VerificationPhase.Test, true, "ok"));

        Assert.True(result.AllPassed);
    }

    [Fact]
    public void AllPassed_OneFail_ReturnsFalse()
    {
        var result = new VerificationResult { ProjectType = "Dotnet" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Build, true, "ok"));
        result.Phases.Add(new PhaseResult(VerificationPhase.Test, false, "1 test failed"));

        Assert.False(result.AllPassed);
    }

    [Fact]
    public void AllPassed_SkippedPhasesCountAsPass()
    {
        var result = new VerificationResult { ProjectType = "Node" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Build, true, "ok"));
        result.Phases.Add(new PhaseResult(VerificationPhase.TypeCheck, true, "", Skipped: true));

        Assert.True(result.AllPassed);
    }

    [Fact]
    public void PhaseResult_Status_ReturnsCorrectString()
    {
        Assert.Equal("PASS", new PhaseResult(VerificationPhase.Build, true, "").Status);
        Assert.Equal("FAIL", new PhaseResult(VerificationPhase.Build, false, "err").Status);
        Assert.Equal("SKIP", new PhaseResult(VerificationPhase.Build, true, "", Skipped: true).Status);
    }

    [Fact]
    public void ToReport_ContainsProjectType()
    {
        var result = new VerificationResult { ProjectType = "Dotnet" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Build, true, "ok"));

        var report = result.ToReport();
        Assert.Contains("Dotnet", report);
        Assert.Contains("All gates passed", report);
    }

    [Fact]
    public void ToReport_IncludesFailureOutput()
    {
        var result = new VerificationResult { ProjectType = "Node" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Test, false, "FAILED: test_foo\nExpected 1 got 2"));

        var report = result.ToReport();
        Assert.Contains("FAILED: test_foo", report);
        Assert.Contains("One or more gates failed", report);
    }

    [Fact]
    public void ToReport_SkippedPhaseShown()
    {
        var result = new VerificationResult { ProjectType = "Go" };
        result.Phases.Add(new PhaseResult(VerificationPhase.Lint, true, "", Skipped: true));

        var report = result.ToReport();
        Assert.Contains("SKIP", report);
    }
}
