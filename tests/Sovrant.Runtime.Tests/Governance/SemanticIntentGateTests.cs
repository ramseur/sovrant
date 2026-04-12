using Sovrant.Api.Routing;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class SemanticIntentGateTests
{
    private readonly SemanticIntentGate _gate = new();

    // ── Ambiguous short inputs → clarification ─────────────────────────

    [Theory]
    [InlineData("test")]
    [InlineData("run")]
    [InlineData("build")]
    [InlineData("fix")]
    [InlineData("check")]
    [InlineData("clean")]
    public async Task ShortAmbiguous_NoClarificationContext_NeedsClarification(string input)
    {
        var result = await _gate.ClassifyAsync(input, conversationDepth: 0);

        Assert.True(result.NeedsClarification);
        Assert.False(result.RequiresTools);
        Assert.NotNull(result.SuggestedClarification);
    }

    [Theory]
    [InlineData("test")]
    [InlineData("run")]
    [InlineData("build")]
    public async Task ShortAmbiguous_DeepConversation_SkipsClarification(string input)
    {
        // In a deep conversation (>2), short messages are likely follow-ups.
        var result = await _gate.ClassifyAsync(input, conversationDepth: 5);

        Assert.False(result.NeedsClarification);
    }

    // ── Conversational messages → no tools ──────────────────────────────

    [Theory]
    [InlineData("hello")]
    [InlineData("thanks")]
    [InlineData("how are you?")]
    [InlineData("what is a monad?")]
    public async Task Conversational_NoTools(string input)
    {
        var result = await _gate.ClassifyAsync(input, conversationDepth: 0);

        Assert.False(result.RequiresTools);
        Assert.False(result.NeedsClarification);
    }

    // ── Clear tool requests → tools required ────────────────────────────

    [Theory]
    [InlineData("read the ./config.json file and check for the auth section")]
    [InlineData("fix the bug in src/auth.cs where the token expires too early")]
    [InlineData("refactor the UserService class to extract an interface")]
    [InlineData("debug the null reference exception in OrderController.cs")]
    [InlineData("create a new endpoint for user registration")]
    public async Task ClearToolRequest_RequiresTools(string input)
    {
        var result = await _gate.ClassifyAsync(input, conversationDepth: 0);

        Assert.True(result.RequiresTools);
        Assert.False(result.NeedsClarification);
    }

    // ── Empty input → conversational, no tools ──────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public async Task EmptyOrWhitespace_Conversational(string? input)
    {
        if (input is null)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _gate.ClassifyAsync(input!, conversationDepth: 0));
            return;
        }

        var result = await _gate.ClassifyAsync(input, conversationDepth: 0);

        Assert.Equal(IntentClass.Conversation, result.Intent);
        Assert.False(result.RequiresTools);
        Assert.False(result.NeedsClarification);
    }

    // ── Clarification messages are contextual ───────────────────────────

    [Fact]
    public async Task Test_Clarification_HasContextualQuestion()
    {
        var result = await _gate.ClassifyAsync("test", conversationDepth: 0);

        Assert.True(result.NeedsClarification);
        Assert.Contains("test", result.SuggestedClarification!, StringComparison.OrdinalIgnoreCase);
    }

    // ── File path in message → tools required ───────────────────────────

    [Fact]
    public async Task MessageWithFilePath_RequiresTools()
    {
        var result = await _gate.ClassifyAsync(
            "explain what /src/controllers/auth.cs does", conversationDepth: 0);

        Assert.True(result.RequiresTools);
    }
}
