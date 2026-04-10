using Sovrant.Api.Routing;

namespace Sovrant.Api.Tests.Routing;

/// <summary>Tests for <see cref="IntentClassifier"/>.</summary>
public sealed class IntentClassifierTests
{
    // ── Intent detection ────────────────────────────────────────────────

    [Theory]
    [InlineData("what is the capital of France?", IntentClass.SimpleQa)]
    [InlineData("who is Alan Turing?", IntentClass.SimpleQa)]
    [InlineData("how many planets are there?", IntentClass.SimpleQa)]
    public void Classify_SimpleQa(string input, IntentClass expected)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(expected, result.Intent);
        Assert.Equal(ModelTier.Fast, result.RecommendedTier);
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
    [InlineData("add a test for the login flow")]
    public void Classify_CodeGeneration(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.CodeGeneration, result.Intent);
        Assert.Equal(ModelTier.Standard, result.RecommendedTier);
    }

    [Theory]
    [InlineData("review this PR")]
    [InlineData("explain what this function does")]
    [InlineData("walk me through this code")]
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
    [InlineData("analyse the performance metrics from last week")]
    [InlineData("summarize the findings from the user research")]
    public void Classify_Analysis(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Analysis, result.Intent);
    }

    [Theory]
    [InlineData("write a story about a robot learning to code")]
    [InlineData("brainstorm names for the new product")]
    public void Classify_Creative(string input)
    {
        var result = IntentClassifier.Classify(input);
        Assert.Equal(IntentClass.Creative, result.Intent);
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
        // Very long text with everything
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
