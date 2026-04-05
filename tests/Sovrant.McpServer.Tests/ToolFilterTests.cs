namespace Sovrant.McpServer.Tests;

public sealed class ToolFilterTests
{
    [Fact]
    public void IsAllowed_NullAllowList_AllowsEverything()
    {
        Assert.True(ToolFilter.IsAllowed("ReadFile", null));
        Assert.True(ToolFilter.IsAllowed("Bash", null));
        Assert.True(ToolFilter.IsAllowed("anything", null));
    }

    [Fact]
    public void IsAllowed_AllowList_FiltersCorrectly()
    {
        var allow = new HashSet<string>(StringComparer.Ordinal) { "ReadFile", "Bash" };
        Assert.True(ToolFilter.IsAllowed("ReadFile", allow));
        Assert.True(ToolFilter.IsAllowed("Bash", allow));
        Assert.False(ToolFilter.IsAllowed("WriteFile", allow));
    }

    [Fact]
    public void IsAllowed_ChatToolAlwaysAllowed()
    {
        var allow = new HashSet<string>(StringComparer.Ordinal) { "ReadFile" };
        Assert.True(ToolFilter.IsAllowed("chat", allow));
    }

    [Fact]
    public void IsAllowed_CaseSensitive()
    {
        var allow = new HashSet<string>(StringComparer.Ordinal) { "ReadFile" };
        Assert.False(ToolFilter.IsAllowed("readfile", allow));
        Assert.False(ToolFilter.IsAllowed("READFILE", allow));
    }

    [Fact]
    public void GetAllowList_ReturnsNullWhenEnvNotSet()
    {
        // Save and clear.
        var prev = Environment.GetEnvironmentVariable("SOVRANT_MCP_TOOLS");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", null);
            Assert.Null(ToolFilter.GetAllowList());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", prev);
        }
    }

    [Fact]
    public void GetAllowList_ParsesCommaSeparated()
    {
        var prev = Environment.GetEnvironmentVariable("SOVRANT_MCP_TOOLS");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", "ReadFile, Bash , Grep");
            var list = ToolFilter.GetAllowList();
            Assert.NotNull(list);
            Assert.Contains("ReadFile", list);
            Assert.Contains("Bash", list);
            Assert.Contains("Grep", list);
            Assert.Equal(3, list.Count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", prev);
        }
    }

    [Fact]
    public void GetAllowList_EmptyStringReturnsNull()
    {
        var prev = Environment.GetEnvironmentVariable("SOVRANT_MCP_TOOLS");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", "");
            Assert.Null(ToolFilter.GetAllowList());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", prev);
        }
    }

    [Fact]
    public void GetAllowList_WhitespaceOnlyReturnsNull()
    {
        var prev = Environment.GetEnvironmentVariable("SOVRANT_MCP_TOOLS");
        try
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", "   ");
            Assert.Null(ToolFilter.GetAllowList());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_MCP_TOOLS", prev);
        }
    }
}
