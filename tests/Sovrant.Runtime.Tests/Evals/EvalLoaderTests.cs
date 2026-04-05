using Sovrant.Runtime.Evals;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class EvalLoaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-eval-loader-{Guid.NewGuid():N}");

    public EvalLoaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void LoadAll_NoDirectory_ReturnsEmpty()
    {
        var result = EvalLoader.LoadAll([Path.Combine(_tempDir, "nonexistent")]);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadAll_EmptyDirectory_ReturnsEmpty()
    {
        var result = EvalLoader.LoadAll([_tempDir]);
        Assert.Empty(result);
    }

    [Fact]
    public void LoadFile_SingleEvalDefinition()
    {
        var file = Path.Combine(_tempDir, "test-eval.json");
        File.WriteAllText(file, """
        {
            "name": "detect-sqli",
            "prompt": "Review this code for SQL injection",
            "grader": "code",
            "check": "grep -q 'SQL injection' {output}",
            "pass_threshold": 1
        }
        """);

        var suite = EvalLoader.LoadFile(file);

        Assert.NotNull(suite);
        Assert.Equal("test-eval", suite.Name);
        Assert.Single(suite.Evals);
        Assert.Equal("detect-sqli", suite.Evals[0].Name);
        Assert.Equal(GraderType.Code, suite.Evals[0].Grader.Type);
        Assert.Contains("grep", suite.Evals[0].Grader.Command);
    }

    [Fact]
    public void LoadFile_SuiteDefinition()
    {
        var file = Path.Combine(_tempDir, "security-suite.json");
        File.WriteAllText(file, """
        {
            "name": "security-evals",
            "description": "Security vulnerability detection evals",
            "tags": ["security"],
            "evals": [
                {
                    "name": "detect-sqli",
                    "prompt": "Review code for SQL injection",
                    "grader": { "type": "code", "pattern": "SQL injection" }
                },
                {
                    "name": "detect-xss",
                    "prompt": "Review code for XSS",
                    "grader": { "type": "model", "rubric": "Does the output identify XSS vulnerabilities?" }
                }
            ]
        }
        """);

        var suite = EvalLoader.LoadFile(file);

        Assert.NotNull(suite);
        Assert.Equal("security-evals", suite.Name);
        Assert.Equal("Security vulnerability detection evals", suite.Description);
        Assert.Single(suite.Tags);
        Assert.Equal(2, suite.Evals.Count);
        Assert.Equal(GraderType.Code, suite.Evals[0].Grader.Type);
        Assert.Equal(GraderType.Model, suite.Evals[1].Grader.Type);
    }

    [Fact]
    public void LoadFile_GraderObjectFormat()
    {
        var file = Path.Combine(_tempDir, "test.json");
        File.WriteAllText(file, """
        {
            "name": "quality-check",
            "prompt": "Review this code",
            "grader": {
                "type": "model",
                "rubric": "Rate completeness 1-10",
                "model": "gpt-4o"
            },
            "category": "quality",
            "attempts": 3,
            "pass_threshold": 2,
            "tags": ["quality", "review"],
            "description": "Tests review quality"
        }
        """);

        var suite = EvalLoader.LoadFile(file);

        Assert.NotNull(suite);
        var eval = suite.Evals[0];
        Assert.Equal(GraderType.Model, eval.Grader.Type);
        Assert.Equal("Rate completeness 1-10", eval.Grader.Rubric);
        Assert.Equal("gpt-4o", eval.Grader.Model);
        Assert.Equal(EvalCategory.Quality, eval.Category);
        Assert.Equal(3, eval.Attempts);
        Assert.Equal(2, eval.PassThreshold);
        Assert.Equal(2, eval.Tags.Count);
        Assert.Equal("Tests review quality", eval.Description);
    }

    [Fact]
    public void LoadFile_HumanGrader()
    {
        var file = Path.Combine(_tempDir, "human.json");
        File.WriteAllText(file, """
        {
            "name": "human-review",
            "prompt": "Write a haiku about testing",
            "grader": "human"
        }
        """);

        var suite = EvalLoader.LoadFile(file);

        Assert.NotNull(suite);
        Assert.Equal(GraderType.Human, suite.Evals[0].Grader.Type);
    }

    [Fact]
    public void LoadFile_InvalidJson_ReturnsNull()
    {
        var file = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(file, "not json{{{");

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => EvalLoader.LoadFile(file));
    }

    [Fact]
    public void LoadFile_MissingRequiredFields_ReturnsNull()
    {
        var file = Path.Combine(_tempDir, "missing.json");
        File.WriteAllText(file, """{ "name": "no-prompt" }""");

        var suite = EvalLoader.LoadFile(file);
        Assert.Null(suite);
    }

    [Fact]
    public void LoadAll_MultipleDirectories_MergesResults()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        File.WriteAllText(Path.Combine(dir1, "a.json"), """
        { "name": "eval-a", "prompt": "test a", "grader": "code" }
        """);
        File.WriteAllText(Path.Combine(dir2, "b.json"), """
        { "name": "eval-b", "prompt": "test b", "grader": "code" }
        """);

        var suites = EvalLoader.LoadAll([dir1, dir2]);

        Assert.Equal(2, suites.Count);
    }

    [Fact]
    public void LoadFile_DefaultGrader_WhenNoGraderSpecified()
    {
        var file = Path.Combine(_tempDir, "default.json");
        File.WriteAllText(file, """
        {
            "name": "basic",
            "prompt": "do something",
            "pattern": "expected output"
        }
        """);

        var suite = EvalLoader.LoadFile(file);

        Assert.NotNull(suite);
        Assert.Equal(GraderType.Code, suite.Evals[0].Grader.Type);
        Assert.Equal("expected output", suite.Evals[0].Grader.Pattern);
    }

    [Fact]
    public void LoadFile_Attempts_DefaultsToOne()
    {
        var file = Path.Combine(_tempDir, "defaults.json");
        File.WriteAllText(file, """
        { "name": "minimal", "prompt": "test", "grader": "code" }
        """);

        var suite = EvalLoader.LoadFile(file);
        Assert.NotNull(suite);
        Assert.Equal(1, suite.Evals[0].Attempts);
        Assert.Equal(1, suite.Evals[0].PassThreshold);
    }

    [Fact]
    public void LoadFile_CommentsAllowed()
    {
        var file = Path.Combine(_tempDir, "comments.json");
        File.WriteAllText(file, """
        {
            // This is a comment
            "name": "with-comments",
            "prompt": "test",
            "grader": "code"
        }
        """);

        var suite = EvalLoader.LoadFile(file);
        Assert.NotNull(suite);
        Assert.Equal("with-comments", suite.Evals[0].Name);
    }
}
