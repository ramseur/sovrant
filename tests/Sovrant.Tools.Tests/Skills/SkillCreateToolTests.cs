using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Knowledge;
using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillCreateToolTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-skillcreate-{Guid.NewGuid():N}");
    private readonly string _originalDir;

    public SkillCreateToolTests()
    {
        Directory.CreateDirectory(_tempDir);
        _originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        if (Directory.Exists(_tempDir))
            try { Directory.Delete(_tempDir, recursive: true); } catch (IOException) { }
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    private static (SkillCreateTool tool, SkillRegistry registry, FakeSkillStore store) CreateTool()
    {
        var store = new FakeSkillStore();
        var registry = new SkillRegistry(store, NullLogger<SkillRegistry>.Instance);
        var tool = new SkillCreateTool(registry, store);
        return (tool, registry, store);
    }

    [Fact]
    public void Definition_HasCorrectName()
    {
        var (tool, _, _) = CreateTool();
        Assert.Equal("SkillCreate", tool.Definition.Name);
    }

    [Fact]
    public async Task Execute_CreatesDbRow()
    {
        var (tool, registry, _) = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            name = "my-skill",
            description = "My custom skill",
            trigger = "/mine",
            body = "## Steps\n1. Do stuff",
        }));

        Assert.Contains("my-skill", result);
        var skill = registry.TryGetByName("my-skill");
        Assert.NotNull(skill);
        Assert.Equal("/mine", skill.Trigger);
        Assert.Contains("Do stuff", skill.Body);
    }

    [Fact]
    public async Task Execute_RegistersInRegistry()
    {
        var (tool, registry, _) = CreateTool();

        await tool.ExecuteAsync(MakeInput(new
        {
            name = "registered-skill",
            body = "Do things",
        }));

        Assert.NotNull(registry.TryGetByName("registered-skill"));
    }

    [Fact]
    public async Task Execute_MissingName_ReturnsError()
    {
        var (tool, _, _) = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { body = "stuff" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_MissingBody_ReturnsError()
    {
        var (tool, _, _) = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "no-body" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_PathTraversal_ReturnsError()
    {
        var (tool, _, _) = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "../evil", body = "stuff" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_WithAgentsAndTools_StoredInRegistry()
    {
        var (tool, registry, _) = CreateTool();

        await tool.ExecuteAsync(MakeInput(new
        {
            name = "full-skill",
            description = "Full",
            trigger = "/full",
            agents = new[] { "coder", "reviewer" },
            tools = new[] { "Read", "Write" },
            body = "Body",
        }));

        var skill = registry.TryGetByName("full-skill");
        Assert.NotNull(skill);
        Assert.Contains("coder", skill.Agents);
        Assert.Contains("Read", skill.Tools);
    }
}
