using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillRunnerTests
{
    private static SkillRunner CreateRunner(params (string slug, string trigger, string body, string[]? tools, string[]? agents)[] skills)
    {
        var store = new FakeSkillStore();
        foreach (var (slug, trigger, body, toolArr, agentArr) in skills)
        {
            var now = DateTimeOffset.UtcNow;
            store.Seed(new KnowledgePage(
                $"skill_{slug}", "skills", slug, slug, "", "BuiltIn", body, "", now, now,
                Trigger: trigger,
                Tools:   toolArr is { Length: > 0 } ? System.Text.Json.JsonSerializer.Serialize(toolArr) : null,
                Agents:  agentArr is { Length: > 0 } ? System.Text.Json.JsonSerializer.Serialize(agentArr) : null));
        }
        var registry = new SkillRegistry(store, NullLogger<SkillRegistry>.Instance);
        return new SkillRunner(registry);
    }

    private static SkillRunner TddRunner() => CreateRunner(
        ("tdd-workflow", "/tdd", "## Red-Green-Refactor\nWrite a failing test first.", ["Bash", "Read"], ["coder"]),
        ("deep-research", "/research", "Systematic research workflow.", [], []));

    [Fact]
    public void Execute_ByName_ReturnsPrompt()
    {
        var runner = TddRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("# Skill: tdd-workflow", result);
        Assert.Contains("Red-Green-Refactor", result);
    }

    [Fact]
    public void Execute_ByTrigger_ReturnsPrompt()
    {
        var runner = TddRunner();
        var result = runner.Execute("/tdd");

        Assert.Contains("# Skill: tdd-workflow", result);
    }

    [Fact]
    public void Execute_NotFound_ReturnsError()
    {
        var runner = TddRunner();
        var result = runner.Execute("nonexistent");

        Assert.StartsWith("Error:", result);
    }

    [Fact]
    public void Execute_WithArguments_Appended()
    {
        var runner = TddRunner();
        var result = runner.Execute("tdd-workflow", "implement a sorting function");

        Assert.Contains("## Arguments", result);
        Assert.Contains("implement a sorting function", result);
    }

    [Fact]
    public void Execute_WithArgumentsSubstitution()
    {
        var runner = CreateRunner(("sub-test", "/sub", "Do $ARGUMENTS now", [], []));
        var result = runner.Execute("sub-test", "the thing");

        Assert.Contains("Do the thing now", result);
        Assert.DoesNotContain("$ARGUMENTS", result);
    }

    [Fact]
    public void Execute_IncludesToolConstraints()
    {
        var runner = TddRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("**Allowed tools:**", result);
        Assert.Contains("Bash", result);
    }

    [Fact]
    public void Execute_IncludesAgentHints()
    {
        var runner = TddRunner();
        var result = runner.Execute("tdd-workflow");

        Assert.Contains("**Agent templates:**", result);
    }

    [Fact]
    public void ListSkills_ReturnsAll()
    {
        var runner = TddRunner();
        var result = runner.ListSkills();

        Assert.Contains("## Available Skills", result);
        Assert.Contains("tdd-workflow", result);
        Assert.Contains("deep-research", result);
        Assert.Contains("/tdd", result);
    }

    [Fact]
    public void ListSkills_EmptyRegistry_DoesNotThrow()
    {
        var store = new FakeSkillStore();
        var registry = new SkillRegistry(store, NullLogger<SkillRegistry>.Instance);
        var runner = new SkillRunner(registry);

        var result = runner.ListSkills();
        Assert.NotNull(result);
    }
}
