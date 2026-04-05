using System.Text.Json;
using Sovrant.Agents.Teams;
using Sovrant.Tools.Team;

namespace Sovrant.Agents.Tests.Tools.Team;

public sealed class TeamCreateToolTests
{
    [Fact]
    public async Task ExecuteAsync_Creates_Member_With_Valid_Input()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamCreateTool(registry);

        var input = JsonDocument.Parse("""{"name":"alice","prompt":"You are a coder.","role":"coder"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("alice", result, StringComparison.Ordinal);
        Assert.Contains("created", result, StringComparison.Ordinal);

        var members = registry.GetAllMembers();
        Assert.Single(members);
        Assert.Equal("alice", members[0].Name);
        Assert.Equal(Sovrant.Agents.Models.AgentRole.Coder, members[0].Role);
    }

    [Fact]
    public async Task ExecuteAsync_Missing_Name_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamCreateTool(registry);

        var input = JsonDocument.Parse("""{"prompt":"test"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("name", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Missing_Prompt_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamCreateTool(registry);

        var input = JsonDocument.Parse("""{"name":"bob"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("prompt", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Invalid_Role_Defaults_To_General()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamCreateTool(registry);

        var input = JsonDocument.Parse("""{"name":"charlie","prompt":"test","role":"invalid_role"}""").RootElement;
        await tool.ExecuteAsync(input);

        var members = registry.GetAllMembers();
        Assert.Single(members);
        Assert.Equal(Sovrant.Agents.Models.AgentRole.General, members[0].Role);
    }

    [Fact]
    public async Task ExecuteAsync_With_AllowedTools()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamCreateTool(registry);

        var input = JsonDocument.Parse("""{"name":"dave","prompt":"test","allowed_tools":["ReadFile","GlobTool"]}""").RootElement;
        await tool.ExecuteAsync(input);

        var member = registry.GetAllMembers()[0];
        Assert.NotNull(member.AllowedTools);
        Assert.Equal(2, member.AllowedTools.Count);
    }
}
