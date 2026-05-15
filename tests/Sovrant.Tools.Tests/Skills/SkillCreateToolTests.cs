using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
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
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup — file may still be locked by registry
        }
    }

    private static JsonElement MakeInput(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    private static SkillCreateTool CreateTool(SkillRegistry? registry = null)
    {
        registry ??= new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        return new SkillCreateTool(registry);
    }

    [Fact]
    public void Definition_HasCorrectName()
    {
        var tool = CreateTool();
        Assert.Equal("SkillCreate", tool.Definition.Name);
    }

    [Fact]
    public async Task Execute_CreatesFile()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new
        {
            name = "my-skill",
            description = "My custom skill",
            trigger = "/mine",
            body = "## Steps\n1. Do stuff",
        }));

        Assert.Contains("my-skill", result);

        var filePath = Path.Combine(_tempDir, ".sovrant", "skills", "my-skill.md");
        Assert.True(File.Exists(filePath));

        var content = File.ReadAllText(filePath);
        Assert.Contains("name: my-skill", content);
        Assert.Contains("trigger: /mine", content);
        Assert.Contains("## Steps", content);
    }

    [Fact]
    public async Task Execute_RegistersInRegistry()
    {
        var registry = new SkillRegistry(NullLogger<SkillRegistry>.Instance);
        var tool = new SkillCreateTool(registry);

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
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { body = "stuff" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_MissingBody_ReturnsError()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "no-body" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_PathTraversal_ReturnsError()
    {
        var tool = CreateTool();
        var result = await tool.ExecuteAsync(MakeInput(new { name = "../evil", body = "stuff" }));
        Assert.StartsWith("Error", result);
    }

    [Fact]
    public async Task Execute_WithAgentsAndTools_IncludesInFrontmatter()
    {
        var tool = CreateTool();
        await tool.ExecuteAsync(MakeInput(new
        {
            name = "full-skill",
            description = "Full",
            trigger = "/full",
            agents = new[] { "coder", "reviewer" },
            tools = new[] { "Read", "Write" },
            body = "Body",
        }));

        var filePath = Path.Combine(_tempDir, ".sovrant", "skills", "full-skill.md");
        var content = File.ReadAllText(filePath);
        Assert.Contains("agents: [coder, reviewer]", content);
        Assert.Contains("tools: [Read, Write]", content);
    }
}
