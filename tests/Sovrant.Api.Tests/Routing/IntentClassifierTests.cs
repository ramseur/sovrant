using Sovrant.Api.Routing;

namespace Sovrant.Api.Tests.Routing;

/// <summary>Tests for <see cref="IntentClassifier"/>.</summary>
public sealed class IntentClassifierTests
{
    // ── Intent detection ────────────────────────────────────────────────

    [Theory]
    [InlineData("what is the capital of France?", IntentClass.Explain)]
    [InlineData("who is Alan Turing?", IntentClass.Explain)]
    [InlineData("how many planets are there?", IntentClass.Explain)]
    [InlineData("explain what a monad is", IntentClass.Explain)]
    [InlineData("teach me about design patterns", IntentClass.Explain)]
    public void Classify_Explain(string input, IntentClass expected)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(expected, result.Intent);
    }

    [Theory]
    [InlineData("refactor this module into smaller classes")]
    [InlineData("restructure the code to use the strategy pattern")]
    [InlineData("extract method from this large function")]
    [InlineData("clean up this file")]
    public void Classify_Refactor(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Refactor, result.Intent);
        Assert.Equal(ModelTier.High, result.RecommendedTier);
    }

    [Theory]
    [InlineData("plan the implementation for a new auth system")]
    [InlineData("design the architecture for the payment module")]
    [InlineData("how should we approach building the notification system?")]
    [InlineData("break this down into tasks")]
    public void Classify_Planning(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Planning, result.Intent);
        Assert.Equal(ModelTier.High, result.RecommendedTier);
    }

    [Theory]
    [InlineData("write a function that validates email addresses")]
    [InlineData("create a new endpoint for user registration")]
    [InlineData("implement the retry logic for HTTP requests")]
    [InlineData("build a REST API for user management")]
    [InlineData("make an app that tracks expenses")]
    public void Classify_CodeGeneration(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.CodeGeneration, result.Intent);
        Assert.Equal(ModelTier.Standard, result.RecommendedTier);
    }

    [Theory]
    [InlineData("add a test for the login flow")]
    [InlineData("write unit tests for the auth module")]
    [InlineData("create integration tests for the API")]
    [InlineData("test this function")]
    public void Classify_TestGeneration(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.TestGeneration, result.Intent);
    }

    [Theory]
    [InlineData("review this code")]
    [InlineData("walk me through this code")]
    [InlineData("check this implementation")]
    public void Classify_CodeReview(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.CodeReview, result.Intent);
    }

    [Theory]
    [InlineData("debug this error: NullReferenceException at line 42")]
    [InlineData("fix this bug where the login page crashes")]
    [InlineData("this test is failing with an assertion error")]
    public void Classify_Debugging(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Debugging, result.Intent);
    }

    [Theory]
    [InlineData("create a README for this project")]
    [InlineData("write API documentation for the auth module")]
    [InlineData("document this function")]
    public void Classify_DocGeneration(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.DocGeneration, result.Intent);
    }

    [Theory]
    [InlineData("write a technical spec for the new feature")]
    [InlineData("draft an architecture document")]
    [InlineData("create an RFC for the new API")]
    public void Classify_SpecDrafting(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.SpecDrafting, result.Intent);
    }

    [Theory]
    [InlineData("create a report on the test results")]
    [InlineData("summarize the findings from the user research")]
    [InlineData("generate a summary of this codebase")]
    public void Classify_ReportGeneration(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.ReportGeneration, result.Intent);
    }

    [Theory]
    [InlineData("find all files that reference UserService")]
    [InlineData("search for the login handler")]
    [InlineData("grep for TODO comments")]
    public void Classify_FileSearch(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.FileSearch, result.Intent);
    }

    [Theory]
    [InlineData("delete the temp folder")]
    [InlineData("rename this file to config.yaml")]
    [InlineData("move the tests to a separate directory")]
    public void Classify_FileManage(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.FileManage, result.Intent);
    }

    [Theory]
    [InlineData("run the build command")]
    [InlineData("execute npm install")]
    [InlineData("run dotnet test")]
    public void Classify_ShellExec(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.ShellExec, result.Intent);
    }

    [Theory]
    [InlineData("set up the project dependencies")]
    [InlineData("configure the CI pipeline")]
    [InlineData("add a nuget package for logging")]
    public void Classify_EnvConfig(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.EnvConfig, result.Intent);
    }

    [Theory]
    [InlineData("commit these changes")]
    [InlineData("create a pull request")]
    [InlineData("merge the feature branch")]
    public void Classify_GitOps(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.GitOps, result.Intent);
    }

    [Theory]
    [InlineData("compare React vs Vue for this project")]
    [InlineData("what are the pros and cons of microservices?")]
    [InlineData("which should I use, Postgres or MongoDB?")]
    public void Classify_Compare(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Compare, result.Intent);
    }

    [Theory]
    [InlineData("research best practices for error handling")]
    [InlineData("investigate the state of the art in caching")]
    [InlineData("look into current approaches for auth")]
    public void Classify_Research(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Research, result.Intent);
    }

    [Theory]
    [InlineData("change the color to blue in the header")]
    [InlineData("update the timeout value from 30 to 60")]
    public void Classify_CodeEdit(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.CodeEdit, result.Intent);
    }

    [Fact]
    public void Classify_NullOrEmpty_ReturnsConversation()
    {
        Assert.Equal(IntentClass.Conversation, IntentClassifier.Classify(null).Intent);
        Assert.Equal(IntentClass.Conversation, IntentClassifier.Classify("").Intent);
        Assert.Equal(IntentClass.Conversation, IntentClassifier.Classify("   ").Intent);
    }

    [Fact]
    public void Classify_GenericText_ReturnsConversation()
    {
        var result = IntentClassifier.Classify("hello, nice to meet you");
        Assert.Equal(IntentClass.Conversation, result.Intent);
        Assert.Equal(ModelTier.Fast, result.RecommendedTier);
    }

    // ── Complexity scoring ──────────────────────────────────────────────

    [Fact]
    public void EstimateComplexity_ShortText_LowScore()
    {
        var score = IntentClassifier.EstimateComplexity("fix the bug", 0);
        Assert.InRange(score, 0f, 0.2f);
    }

    [Fact]
    public void EstimateComplexity_WithCodeBlock_IncreasesScore()
    {
        var text = "fix this:\n```csharp\nvar x = 1;\n```";
        var score = IntentClassifier.EstimateComplexity(text, 0);
        Assert.True(score >= 0.15f, $"Expected >= 0.15 but got {score}");
    }

    [Fact]
    public void EstimateComplexity_MultiStep_IncreasesScore()
    {
        var text = "first do this, then do that, finally run the tests";
        var score = IntentClassifier.EstimateComplexity(text, 0);
        Assert.True(score >= 0.1f, $"Expected >= 0.1 but got {score}");
    }

    [Fact]
    public void EstimateComplexity_DeepConversation_IncreasesScore()
    {
        var score = IntentClassifier.EstimateComplexity("continue", 10);
        Assert.True(score >= 0.1f, $"Expected >= 0.1 for deep conversation but got {score}");
    }

    [Fact]
    public void EstimateComplexity_CapsAtOne()
    {
        var text = string.Join(" ", Enumerable.Repeat("first then finally step 1 step 2 step 3", 50))
            + "\n```code\nvar x = 1;\n```"
            + string.Concat(Enumerable.Repeat(" /src/file.cs", 10));

        var score = IntentClassifier.EstimateComplexity(text, 20);
        Assert.InRange(score, 0f, 1.0f);
    }

    // ── Tier mapping ────────────────────────────────────────────────────

    [Fact]
    public void MapTier_HighComplexityCodeGen_Escalates()
    {
        var tier = IntentClassifier.MapTier(IntentClass.CodeGeneration, 0.8f, 0);
        Assert.Equal(ModelTier.High, tier);
    }

    [Fact]
    public void MapTier_LowComplexityCodeGen_Standard()
    {
        var tier = IntentClassifier.MapTier(IntentClass.CodeGeneration, 0.3f, 0);
        Assert.Equal(ModelTier.Standard, tier);
    }

    [Fact]
    public void MapTier_DeepConversation_Escalates()
    {
        var tier = IntentClassifier.MapTier(IntentClass.Conversation, 0.2f, 10);
        Assert.Equal(ModelTier.Standard, tier);
    }
}
