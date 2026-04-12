using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GraduatedToolTiersTests
{
    [Theory]
    [InlineData("ReadFile", ToolTier.Safe)]
    [InlineData("Read", ToolTier.Safe)]
    [InlineData("Glob", ToolTier.Safe)]
    [InlineData("Grep", ToolTier.Safe)]
    [InlineData("ListDirectory", ToolTier.Safe)]
    [InlineData("WebFetch", ToolTier.Safe)]
    [InlineData("AskUserQuestion", ToolTier.Safe)]
    [InlineData("LspHover", ToolTier.Safe)]
    public void SafeTools_ReturnSafeTier(string toolName, ToolTier expected)
    {
        Assert.Equal(expected, GraduatedToolTiers.GetTier(toolName));
    }

    [Theory]
    [InlineData("WriteFile", ToolTier.Moderate)]
    [InlineData("Edit", ToolTier.Moderate)]
    [InlineData("NotebookEdit", ToolTier.Moderate)]
    [InlineData("TaskCreate", ToolTier.Moderate)]
    [InlineData("Artifact", ToolTier.Moderate)]
    [InlineData("LspRename", ToolTier.Moderate)]
    public void ModerateTools_ReturnModerateTier(string toolName, ToolTier expected)
    {
        Assert.Equal(expected, GraduatedToolTiers.GetTier(toolName));
    }

    [Theory]
    [InlineData("Bash", ToolTier.Dangerous)]
    [InlineData("PowerShell", ToolTier.Dangerous)]
    [InlineData("REPL", ToolTier.Dangerous)]
    public void DangerousTools_ReturnDangerousTier(string toolName, ToolTier expected)
    {
        Assert.Equal(expected, GraduatedToolTiers.GetTier(toolName));
    }

    [Theory]
    [InlineData("Agent", ToolTier.Escalation)]
    [InlineData("TeamCreate", ToolTier.Escalation)]
    [InlineData("Mission", ToolTier.Escalation)]
    [InlineData("Swarm", ToolTier.Escalation)]
    [InlineData("SwarmStatus", ToolTier.Escalation)]
    public void EscalationTools_ReturnEscalationTier(string toolName, ToolTier expected)
    {
        Assert.Equal(expected, GraduatedToolTiers.GetTier(toolName));
    }

    [Fact]
    public void UnknownTool_DefaultsToModerate()
    {
        Assert.Equal(ToolTier.Moderate, GraduatedToolTiers.GetTier("SomeUnknownTool"));
    }

    [Fact]
    public void CaseInsensitive()
    {
        Assert.Equal(ToolTier.Dangerous, GraduatedToolTiers.GetTier("bash"));
        Assert.Equal(ToolTier.Dangerous, GraduatedToolTiers.GetTier("BASH"));
    }

    [Fact]
    public void IsAtLeast_Works()
    {
        Assert.True(GraduatedToolTiers.IsAtLeast("Bash", ToolTier.Dangerous));
        Assert.True(GraduatedToolTiers.IsAtLeast("Agent", ToolTier.Dangerous));
        Assert.False(GraduatedToolTiers.IsAtLeast("Read", ToolTier.Moderate));
    }

    [Fact]
    public void AllKnownTools_HaveTierAssignment()
    {
        // Verify we have a reasonable number of tools classified.
        Assert.True(GraduatedToolTiers.All.Count >= 49,
            $"Expected at least 49 tools classified, got {GraduatedToolTiers.All.Count}");
    }
}
