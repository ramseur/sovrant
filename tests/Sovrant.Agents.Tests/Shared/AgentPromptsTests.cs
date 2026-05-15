using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;

namespace Sovrant.Agents.Tests.Shared;

public sealed class AgentPromptsTests
{
    [Theory]
    [InlineData(AgentRole.Planner, "decompose")]
    [InlineData(AgentRole.Coder, "clean")]
    [InlineData(AgentRole.Reviewer, "bugs")]
    [InlineData(AgentRole.Executor, "Run commands")]
    [InlineData(AgentRole.Supervisor, "Coordinate")]
    [InlineData(AgentRole.General, "general-purpose")]
    public void GetSystemPrompt_Returns_Role_Specific_Prompt(AgentRole role, string expectedSubstring)
    {
        var prompt = AgentPrompts.GetSystemPrompt(role);
        Assert.Contains(expectedSubstring, prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSystemPrompt_Appends_CustomInstructions()
    {
        var prompt = AgentPrompts.GetSystemPrompt(AgentRole.General, "Always respond in JSON.");
        Assert.Contains("general-purpose", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Always respond in JSON.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSystemPrompt_Null_CustomInstructions_No_Append()
    {
        var prompt = AgentPrompts.GetSystemPrompt(AgentRole.Coder, null);
        Assert.DoesNotContain("\n\n\n", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void GetSystemPrompt_Blank_CustomInstructions_No_Append()
    {
        var prompt = AgentPrompts.GetSystemPrompt(AgentRole.Coder, "   ");
        Assert.DoesNotContain("   ", prompt, StringComparison.Ordinal);
    }
}
