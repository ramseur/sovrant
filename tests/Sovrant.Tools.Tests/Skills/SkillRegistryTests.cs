using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillRegistryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-skills-{Guid.NewGuid():N}");

    public SkillRegistryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static SkillRegistry CreateRegistry() =>
        new(NullLogger<SkillRegistry>.Instance);

    [Fact]
    public void Constructor_LoadsBuiltInSkills()
    {
        var registry = CreateRegistry();
        // Built-in skills should be loaded from assembly dir
        Assert.True(registry.All.Count > 0, "Expected built-in skills to be loaded");
    }

    [Fact]
    public void TryGetByName_BuiltIn_ReturnsSkill()
    {
        var registry = CreateRegistry();
        var skill = registry.TryGetByName("tdd-workflow");

        Assert.NotNull(skill);
        Assert.Equal("tdd-workflow", skill.Name);
        Assert.Equal("/tdd", skill.Trigger);
    }

    [Fact]
    public void TryGetByTrigger_BuiltIn_ReturnsSkill()
    {
        var registry = CreateRegistry();
        var skill = registry.TryGetByTrigger("/tdd");

        Assert.NotNull(skill);
        Assert.Equal("tdd-workflow", skill.Name);
    }

    [Fact]
    public void TryGetByName_NotFound_ReturnsNull()
    {
        var registry = CreateRegistry();
        Assert.Null(registry.TryGetByName("nonexistent-skill"));
    }

    [Fact]
    public void TryGetByTrigger_NotFound_ReturnsNull()
    {
        var registry = CreateRegistry();
        Assert.Null(registry.TryGetByTrigger("/nonexistent"));
    }

    [Fact]
    public void LoadSkillsFrom_CustomDirectory_LoadsSkills()
    {
        File.WriteAllText(Path.Combine(_tempDir, "custom.md"), """
            ---
            name: custom-skill
            description: A custom skill
            trigger: /custom
            ---
            Custom body
            """);

        var registry = CreateRegistry();
        registry.LoadSkillsFrom(_tempDir);

        var skill = registry.TryGetByName("custom-skill");
        Assert.NotNull(skill);
        Assert.Equal("/custom", skill.Trigger);
    }

    [Fact]
    public void LoadSkillsFrom_OverridesExisting()
    {
        File.WriteAllText(Path.Combine(_tempDir, "tdd-workflow.md"), """
            ---
            name: tdd-workflow
            description: Overridden TDD
            trigger: /tdd
            ---
            Custom TDD body
            """);

        var registry = CreateRegistry();
        registry.LoadSkillsFrom(_tempDir);

        var skill = registry.TryGetByName("tdd-workflow");
        Assert.NotNull(skill);
        Assert.Equal("Overridden TDD", skill.Description);
    }

    [Fact]
    public void Register_AddsSkill()
    {
        var registry = CreateRegistry();
        var skill = new SkillDefinition("dynamic", "Dynamic skill", "/dynamic", [], [], "Do stuff");

        registry.Register(skill);

        Assert.NotNull(registry.TryGetByName("dynamic"));
        Assert.NotNull(registry.TryGetByTrigger("/dynamic"));
    }

    [Fact]
    public void LoadSkillsFrom_InvalidFiles_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "bad.md"), "No frontmatter here");

        var registry = CreateRegistry();
        var countBefore = registry.All.Count;
        registry.LoadSkillsFrom(_tempDir);

        Assert.Equal(countBefore, registry.All.Count);
    }

    [Fact]
    public void All_ContainsAtLeast32BuiltInSkills()
    {
        var registry = CreateRegistry();
        Assert.True(registry.All.Count >= 32, $"Expected at least 32 built-in skills, got {registry.All.Count}");
    }
}
