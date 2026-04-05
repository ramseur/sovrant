using Sovrant.Tools.Skills;

namespace Sovrant.Tools.Tests.Skills;

public sealed class SkillParserTests
{
    private const string ValidSkill = """
        ---
        name: test-skill
        description: A test skill
        trigger: /test
        agents: [coder, reviewer]
        tools: [Read, Write, Bash]
        ---

        # Test Skill

        ## Steps
        1. Do the thing
        2. Verify the thing
        """;

    [Fact]
    public void Parse_ValidSkill_ReturnsDefinition()
    {
        var skill = SkillParser.Parse(ValidSkill);

        Assert.NotNull(skill);
        Assert.Equal("test-skill", skill.Name);
        Assert.Equal("A test skill", skill.Description);
        Assert.Equal("/test", skill.Trigger);
        Assert.Equal(2, skill.Agents.Count);
        Assert.Contains("coder", skill.Agents);
        Assert.Contains("reviewer", skill.Agents);
        Assert.Equal(3, skill.Tools.Count);
        Assert.Contains("Read", skill.Tools);
        Assert.Contains("Bash", skill.Tools);
        Assert.Contains("# Test Skill", skill.Body);
    }

    [Fact]
    public void Parse_Null_ReturnsNull() => Assert.Null(SkillParser.Parse(null!));

    [Fact]
    public void Parse_Empty_ReturnsNull() => Assert.Null(SkillParser.Parse(""));

    [Fact]
    public void Parse_NoFrontmatter_ReturnsNull() =>
        Assert.Null(SkillParser.Parse("Just some text without frontmatter"));

    [Fact]
    public void Parse_NoClosingDashes_ReturnsNull() =>
        Assert.Null(SkillParser.Parse("---\nname: foo\nNo closing dashes"));

    [Fact]
    public void Parse_NoName_ReturnsNull() =>
        Assert.Null(SkillParser.Parse("---\ndescription: no name\n---\nbody"));

    [Fact]
    public void Parse_MinimalFrontmatter_Works()
    {
        var skill = SkillParser.Parse("---\nname: minimal\n---\nBody text");

        Assert.NotNull(skill);
        Assert.Equal("minimal", skill.Name);
        Assert.Empty(skill.Description);
        Assert.Empty(skill.Trigger);
        Assert.Empty(skill.Agents);
        Assert.Empty(skill.Tools);
        Assert.Equal("Body text", skill.Body);
    }

    [Fact]
    public void Parse_EmptyBody_UsesDefaultPrompt()
    {
        var skill = SkillParser.Parse("---\nname: empty-body\n---\n");

        Assert.NotNull(skill);
        Assert.Contains("empty-body", skill.Body);
    }

    [Fact]
    public void Parse_BareListSyntax_Works()
    {
        var skill = SkillParser.Parse("---\nname: bare\ntools: Read, Write\n---\nbody");

        Assert.NotNull(skill);
        Assert.Equal(2, skill.Tools.Count);
        Assert.Contains("Read", skill.Tools);
        Assert.Contains("Write", skill.Tools);
    }

    [Fact]
    public void ParseList_BracketSyntax()
    {
        var result = SkillParser.ParseList("[A, B, C]");
        Assert.Equal(3, result.Count);
        Assert.Equal("A", result[0]);
        Assert.Equal("C", result[2]);
    }

    [Fact]
    public void ParseList_BareSyntax()
    {
        var result = SkillParser.ParseList("X, Y");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ParseList_Empty()
    {
        var result = SkillParser.ParseList("[]");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseFile_NonexistentFile_ReturnsNull() =>
        Assert.Null(SkillParser.ParseFile("/nonexistent/path.md"));
}
