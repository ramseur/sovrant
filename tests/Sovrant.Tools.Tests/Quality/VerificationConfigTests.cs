using Sovrant.Tools.Quality;

namespace Sovrant.Tools.Tests.Quality;

public sealed class VerificationConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-verify-cfg-{Guid.NewGuid():N}");

    public VerificationConfigTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var config = VerificationConfig.Load(_tempDir);

        Assert.Equal(60, config.CoverageThreshold);
        Assert.Equal("warning", config.LintSeverity);
        Assert.Empty(config.SkipPhases);
        Assert.Null(config.BuildCommand);
        Assert.Null(config.TestCommand);
    }

    [Fact]
    public void Load_ValidJson_ParsesAllFields()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "verify.json"), """
        {
            "coverage_threshold": 80,
            "lint_severity": "error",
            "skip_phases": ["TypeCheck", "SecurityScan"],
            "build_command": "make build",
            "test_command": "make test",
            "lint_command": "make lint",
            "typecheck_command": "make typecheck",
            "security_command": "make security"
        }
        """);

        var config = VerificationConfig.Load(_tempDir);

        Assert.Equal(80, config.CoverageThreshold);
        Assert.Equal("error", config.LintSeverity);
        Assert.Equal(2, config.SkipPhases.Count);
        Assert.Contains("TypeCheck", config.SkipPhases);
        Assert.Contains("SecurityScan", config.SkipPhases);
        Assert.Equal("make build", config.BuildCommand);
        Assert.Equal("make test", config.TestCommand);
        Assert.Equal("make lint", config.LintCommand);
        Assert.Equal("make typecheck", config.TypeCheckCommand);
        Assert.Equal("make security", config.SecurityCommand);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "verify.json"), "not valid json{{{");

        var config = VerificationConfig.Load(_tempDir);

        Assert.Equal(60, config.CoverageThreshold);
    }

    [Theory]
    [InlineData("Build", VerificationPhase.Build, true)]
    [InlineData("build", VerificationPhase.Build, true)]
    [InlineData("Test", VerificationPhase.Test, true)]
    [InlineData("Lint", VerificationPhase.Build, false)]
    public void ShouldSkip_RespectsConfiguredPhases(string skipPhase, VerificationPhase check, bool expected)
    {
        var config = new VerificationConfig();
        config.SkipPhases.Add(skipPhase);
        Assert.Equal(expected, config.ShouldSkip(check));
    }
}
