using Sovrant.Runtime.Evals;
using Sovrant.Runtime.Evals.Graders;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class CodeGraderTests
{
    private readonly CodeGrader _grader = new();

    [Fact]
    public async Task PatternOnly_Matches_ReturnsPass()
    {
        var eval = MakeEval(pattern: "hello world");
        var result = await _grader.GradeAsync(eval, "this contains hello world indeed");

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task PatternOnly_NoMatch_ReturnsFail()
    {
        var eval = MakeEval(pattern: "missing text");
        var result = await _grader.GradeAsync(eval, "this does not contain it");

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task PatternOnly_RegexSupported()
    {
        var eval = MakeEval(pattern: @"error\s+\d{3}");
        var result = await _grader.GradeAsync(eval, "got error 404 from server");

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Command_ExitCodeZero_Passes()
    {
        var command = OperatingSystem.IsWindows() ? "echo OK" : "echo OK";
        var eval = MakeEval(command: command);
        var result = await _grader.GradeAsync(eval, "some output");

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Command_NonZeroExitCode_Fails()
    {
        var command = OperatingSystem.IsWindows() ? "cmd /c exit 1" : "false";
        var eval = MakeEval(command: command);
        var result = await _grader.GradeAsync(eval, "some output");

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task Command_With_OutputFile_Substitution()
    {
        // {output} is replaced with a temp file path containing the eval output
        var command = OperatingSystem.IsWindows()
            ? "findstr /c:\"expected text\" {output}"
            : "grep -q 'expected text' {output}";
        var eval = MakeEval(command: command);
        var result = await _grader.GradeAsync(eval, "this has expected text in it");

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task Command_With_OutputFile_NoMatch_Fails()
    {
        var command = OperatingSystem.IsWindows()
            ? "findstr /c:\"nonexistent\" {output}"
            : "grep -q 'nonexistent' {output}";
        var eval = MakeEval(command: command);
        var result = await _grader.GradeAsync(eval, "this does not have the text");

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task NoCommandNoPattern_AutoPasses()
    {
        var eval = MakeEval();
        var result = await _grader.GradeAsync(eval, "anything");

        Assert.True(result.Passed);
        Assert.Contains("auto-pass", result.Output);
    }

    [Fact]
    public async Task Duration_IsTracked()
    {
        var eval = MakeEval(pattern: "test");
        var result = await _grader.GradeAsync(eval, "test");

        Assert.True(result.Duration >= TimeSpan.Zero);
    }

    private static EvalDefinition MakeEval(string? command = null, string? pattern = null) => new()
    {
        Name = "test-eval",
        Prompt = "test",
        Grader = new GraderConfig
        {
            Type = GraderType.Code,
            Command = command,
            Pattern = pattern,
        },
    };
}
