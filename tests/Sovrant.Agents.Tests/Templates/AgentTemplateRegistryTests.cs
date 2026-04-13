using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Shared;
using Sovrant.Agents.Templates;
using Sovrant.Runtime.Config;

namespace Sovrant.Agents.Tests.Templates;

/// <summary>Tests for <see cref="AgentTemplateRegistry"/>.</summary>
public sealed class AgentTemplateRegistryTests
{
    private static AgentTemplateRegistry CreateRegistry() =>
        new(NullLogger<AgentTemplateRegistry>.Instance);

    // ── Built-in count ───────────────────────────────────────────────────────

    [Fact]
    public void All_Returns25BuiltInTemplates()
    {
        var registry = CreateRegistry();
        Assert.Equal(25, registry.All.Count);
    }

    // ── Lookup ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("planner")]
    [InlineData("architect")]
    [InlineData("researcher")]
    [InlineData("chief-of-staff")]
    [InlineData("content-writer")]
    [InlineData("data-analyst")]
    [InlineData("project-manager")]
    [InlineData("doc-updater")]
    [InlineData("loop-operator")]
    [InlineData("executor")]
    [InlineData("code-reviewer")]
    [InlineData("security-reviewer")]
    [InlineData("tdd-guide")]
    [InlineData("refactor-cleaner")]
    [InlineData("coder")]
    [InlineData("build-error-resolver")]
    [InlineData("e2e-test-runner")]
    [InlineData("database-reviewer")]
    [InlineData("gan-planner")]
    [InlineData("gan-generator")]
    [InlineData("gan-evaluator")]
    [InlineData("prompt-optimizer")]
    [InlineData("sales-intelligence")]
    [InlineData("compliance-reviewer")]
    public void TryGet_BuiltInName_ReturnsTemplate(string name)
    {
        var registry = CreateRegistry();
        var template = registry.TryGet(name);
        Assert.NotNull(template);
        Assert.Equal(name, template.Name);
    }

    [Fact]
    public void TryGet_CaseInsensitive()
    {
        var registry = CreateRegistry();
        Assert.NotNull(registry.TryGet("CODER"));
        Assert.NotNull(registry.TryGet("Security-Reviewer"));
        Assert.NotNull(registry.TryGet("CODE-REVIEWER"));
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsNull()
    {
        var registry = CreateRegistry();
        Assert.Null(registry.TryGet("does-not-exist"));
    }

    // ── Template metadata ────────────────────────────────────────────────────

    [Fact]
    public void SecurityReviewer_HasCorrectMetadata()
    {
        var registry = CreateRegistry();
        var t = registry.TryGet("security-reviewer");

        Assert.NotNull(t);
        Assert.Equal(AgentRole.Reviewer, t.Role);
        Assert.Equal(RecommendedLevel.High, t.RecommendedLevel);
        Assert.Contains("Read", t.AllowedTools);
        Assert.Contains("Grep", t.AllowedTools);
        Assert.Contains("Glob", t.AllowedTools);
        Assert.DoesNotContain("Bash", t.AllowedTools);
        Assert.Contains("OWASP", t.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Coder_HasNoToolRestrictions()
    {
        var registry = CreateRegistry();
        var t = registry.TryGet("coder");

        Assert.NotNull(t);
        Assert.Equal(AgentRole.Coder, t.Role);
        Assert.Equal(RecommendedLevel.Standard, t.RecommendedLevel);
        Assert.Empty(t.AllowedTools);  // empty = all tools available
    }

    [Fact]
    public void Planner_IsHighLevel()
    {
        var registry = CreateRegistry();
        var t = registry.TryGet("planner");
        Assert.NotNull(t);
        Assert.Equal(RecommendedLevel.High, t.RecommendedLevel);
    }

    [Fact]
    public void AllTemplates_HaveNonEmptySystemPrompts()
    {
        var registry = CreateRegistry();
        foreach (var t in registry.All)
            Assert.False(string.IsNullOrWhiteSpace(t.SystemPrompt),
                $"Template '{t.Name}' has an empty system prompt.");
    }

    [Fact]
    public void AllTemplates_HaveUniqueNames()
    {
        var registry = CreateRegistry();
        var names = registry.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // ── User template loading ────────────────────────────────────────────────

    [Fact]
    public void LoadUserTemplates_ValidMarkdown_AddsTemplate()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "my-agent.md"),
                "---\nname: my-agent\nrole: General\nrecommended_level: Standard\nallowed_tools: [Read, Bash]\n---\nYou are a custom agent.");

            var registry = CreateRegistry();
            registry.LoadUserTemplates(dir);

            var t = registry.TryGet("my-agent");
            Assert.NotNull(t);
            Assert.Equal("my-agent", t.Name);
            Assert.Equal(AgentRole.General, t.Role);
            Assert.Equal(RecommendedLevel.Standard, t.RecommendedLevel);
            Assert.Equal(["Read", "Bash"], t.AllowedTools);
            Assert.Equal("You are a custom agent.", t.SystemPrompt);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadUserTemplates_OverridesBuiltIn()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "coder.md"),
                "---\nname: coder\nrole: Coder\nrecommended_level: High\n---\nCustom coder instructions.");

            var registry = CreateRegistry();
            registry.LoadUserTemplates(dir);

            var t = registry.TryGet("coder");
            Assert.NotNull(t);
            Assert.Equal("Custom coder instructions.", t.SystemPrompt);
            Assert.Equal(RecommendedLevel.High, t.RecommendedLevel);  // overridden from Standard
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadUserTemplates_MissingFrontMatter_Skipped()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "bad.md"), "No front matter here.");

            var registry = CreateRegistry();
            var countBefore = registry.All.Count;
            registry.LoadUserTemplates(dir);

            Assert.Equal(countBefore, registry.All.Count);  // not added
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void LoadUserTemplates_NonExistentDirectory_NoThrow()
    {
        var registry = CreateRegistry();
        var ex = Record.Exception(() => registry.LoadUserTemplates("/does/not/exist"));
        Assert.Null(ex);
    }

    // ── Model resolution ─────────────────────────────────────────────────────

    [Fact]
    public void ResolveModel_UserOverride_Wins()
    {
        var config = new SovrantConfig { Model = "default-model" };
        var result = SovrantAgentFactory.ResolveModel("explicit-model", RecommendedLevel.High, config);
        Assert.Equal("explicit-model", result);
    }

    [Fact]
    public void ResolveModel_ModelLevels_UsedWhenNoOverride()
    {
        var config = new SovrantConfig
        {
            Model = "default-model",
            ModelLevels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["High"] = "high-model",
                ["Standard"] = "standard-model",
            },
        };

        Assert.Equal("high-model", SovrantAgentFactory.ResolveModel(null, RecommendedLevel.High, config));
        Assert.Equal("standard-model", SovrantAgentFactory.ResolveModel(null, RecommendedLevel.Standard, config));
    }

    [Fact]
    public void ResolveModel_FallsBackToSessionModel()
    {
        var config = new SovrantConfig { Model = "session-model" };
        var result = SovrantAgentFactory.ResolveModel(null, RecommendedLevel.High, config);
        Assert.Equal("session-model", result);
    }

    [Fact]
    public void ResolveModel_EmptyOverride_FallsThrough()
    {
        var config = new SovrantConfig { Model = "default-model" };
        var result = SovrantAgentFactory.ResolveModel("   ", RecommendedLevel.Standard, config);
        Assert.Equal("default-model", result);
    }
}
