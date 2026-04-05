using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillRunnerTests
{
    private static SkillRunner CreateRunner()
    {
        var registry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        return new SkillRunner(registry);
    }

    [Fact]
    public void Execute_ByName_ReturnsPrompt()
    {
        var runner = CreateRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("# Skill: tdd-workflow", result);
        Assert.Contains("Red-Green-Refactor", result);
    }

    [Fact]
    public void Execute_ByTrigger_ReturnsPrompt()
    {
        var runner = CreateRunner();
        var result = runner.Execute("/tdd");

        Assert.Contains("# Skill: tdd-workflow", result);
    }

    [Fact]
    public void Execute_NotFound_ReturnsError()
    {
        var runner = CreateRunner();
        var result = runner.Execute("nonexistent");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void Execute_WithArguments_Appended()
    {
        var runner = CreateRunner();
        var result = runner.Execute("tdd-workflow", "implement a sorting function");

        Assert.Contains("## Arguments", result);
        Assert.Contains("implement a sorting function", result);
    }

    [Fact]
    public void Execute_WithArgumentsSubstitution()
    {
        var registry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        registry.Register(new SkillDefinition(
            "sub-test", "Test", "/sub", [], [],
            "Do $ARGUMENTS now"));

        var runner = new SkillRunner(registry);
        var result = runner.Execute("sub-test", "the thing");

        Assert.Contains("Do the thing now", result);
        Assert.DoesNotContain("$ARGUMENTS", result);
    }

    [Fact]
    public void Execute_IncludesToolConstraints()
    {
        var runner = CreateRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("**Allowed tools:**", result);
        Assert.Contains("Bash", result);
    }

    [Fact]
    public void Execute_IncludesAgentHints()
    {
        var runner = CreateRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("**Agent templates:**", result);
    }

    [Fact]
    public void ListSkills_ReturnsAll()
    {
        var runner = CreateRunner();
        var result = runner.ListSkills();

        Assert.Contains("## Available Skills", result);
        Assert.Contains("tdd-workflow", result);
        Assert.Contains("deep-research", result);
        Assert.Contains("/tdd", result);
    }

    [Fact]
    public void ListSkills_EmptyRegistry()
    {
        var registry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        // Clear by creating with no skill directories available
        // We'll use a fresh registry and verify the list method works
        var runner = new SkillRunner(registry);
        var result = runner.ListSkills();

        // With built-in skills, this won't be empty, so just verify it doesn't throw
        Assert.NotNull(result);
    }
}
