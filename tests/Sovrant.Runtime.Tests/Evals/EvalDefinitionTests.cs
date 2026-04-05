using Sovrant.Runtime.Evals;

namespace Sovrant.Runtime.Tests.Evals;

public sealed class EvalDefinitionTests
{
    [Fact]
    public void Defaults_Are_Sensible()
    {
        var eval = new EvalDefinition
        {
            Name = "test-eval",
            Prompt = "test prompt",
            Grader = new GraderConfig { Type = GraderType.Code },
        };

        Assert.Equal("test-eval", eval.Name);
        Assert.Equal("test prompt", eval.Prompt);
        Assert.Equal(EvalCategory.Capability, eval.Category);
        Assert.Equal(1, eval.Attempts);
        Assert.Equal(1, eval.PassThreshold);
        Assert.Empty(eval.Tags);
        Assert.Null(eval.Description);
    }

    [Fact]
    public void All_Properties_Can_Be_Set()
    {
        var eval = new EvalDefinition
        {
            Name = "sqli-detect",
            Prompt = "Review this code",
            Category = EvalCategory.Quality,
            Grader = new GraderConfig
            {
                Type = GraderType.Model,
                Rubric = "Check for SQL injection",
                Model = "gpt-4o",
            },
            Attempts = 3,
            PassThreshold = 2,
            Tags = ["security", "coding"],
            Description = "Tests SQL injection detection",
        };

        Assert.Equal(EvalCategory.Quality, eval.Category);
        Assert.Equal(GraderType.Model, eval.Grader.Type);
        Assert.Equal("Check for SQL injection", eval.Grader.Rubric);
        Assert.Equal(3, eval.Attempts);
        Assert.Equal(2, eval.PassThreshold);
        Assert.Equal(2, eval.Tags.Count);
    }

    [Theory]
    [InlineData(GraderType.Code)]
    [InlineData(GraderType.Model)]
    [InlineData(GraderType.Human)]
    public void GraderType_All_Values(GraderType type)
    {
        var config = new GraderConfig { Type = type };
        Assert.Equal(type, config.Type);
    }

    [Theory]
    [InlineData(EvalCategory.Capability)]
    [InlineData(EvalCategory.Regression)]
    [InlineData(EvalCategory.Quality)]
    public void EvalCategory_All_Values(EvalCategory category)
    {
        var eval = new EvalDefinition
        {
            Name = "test",
            Prompt = "test",
            Category = category,
            Grader = new GraderConfig { Type = GraderType.Code },
        };
        Assert.Equal(category, eval.Category);
    }
}
