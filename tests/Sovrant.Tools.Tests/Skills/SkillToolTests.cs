using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillToolTests
{
    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    private static SkillTool CreateTool()
    {
        var store = new FakeSkillStore();
        var now = DateTimeOffset.UtcNow;
        store.Seed(new KnowledgePage("skill_tdd", "skills", "tdd-workflow", "tdd-workflow", "", "BuiltIn",
            "## Red-Green-Refactor\nWrite a failing test first.", "", now, now, Trigger: "/tdd"));
        store.Seed(new KnowledgePage("skill_review", "skills", "code-review", "code-review", "", "BuiltIn",
            "## Code review steps", "", now, now, Trigger: "/review"));
        var registry = new SkillRegistry(store, NullLogger<SkillRegistry>.Instance);
        var runner = new SkillRunner(registry);
        return new SkillTool(runner);
    }

    [Fact]
    public void Definition_HasCorrectName()
    {
        var tool = CreateTool();
        Assert.Equal("Skill", tool.Definition.Name);
    }

    [Fact]
    public async Task Execute_ByName_ReturnsSkillPrompt()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "tdd-workflow" }));

        Assert.Contains("# Skill: tdd-workflow", result);
    }

    [Fact]
    public async Task Execute_ByTrigger_ReturnsSkillPrompt()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "/review" }));

        Assert.Contains("# Skill: code-review", result);
    }

    [Fact]
    public async Task Execute_List_ReturnsAllSkills()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "list" }));

        Assert.Contains("## Available Skills", result);
        Assert.Contains("tdd-workflow", result);
    }

    [Fact]
    public async Task Execute_MissingName_ReturnsError()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { }));

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_NotFound_ReturnsError()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "nonexistent" }));

        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_WithArgs_PassesThrough()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "tdd-workflow", args = "sorting algorithm" }));

        Assert.Contains("sorting algorithm", result);
    }
}
