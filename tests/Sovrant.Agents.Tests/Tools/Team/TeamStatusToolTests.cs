using System.Text.Json;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Tools.Team;

namespace Sovrant.Agents.Tests.Tools.Team;

public sealed class TeamStatusToolTests
{
    [Fact]
    public async Task ExecuteAsync_Empty_Team_Returns_Empty_Array()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamStatusTool(registry);

        var input = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Equal("[]", result);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_All_Members()
    {
        var registry = new InMemoryTeamRegistry();
        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "a", Name = "alice", Role = AgentRole.Coder, SystemPrompt = "test",
        });
        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "b", Name = "bob", Role = AgentRole.Reviewer, SystemPrompt = "test",
        });

        var tool = new TeamStatusTool(registry);
        var input = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("alice", result, StringComparison.Ordinal);
        Assert.Contains("bob", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Specific_Member()
    {
        var registry = new InMemoryTeamRegistry();
        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "abc", Name = "alice", Role = AgentRole.Coder, SystemPrompt = "test",
        });

        var tool = new TeamStatusTool(registry);
        var input = JsonDocument.Parse("""{"member_id":"abc"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("alice", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Unknown_Member_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var tool = new TeamStatusTool(registry);

        var input = JsonDocument.Parse("""{"member_id":"nonexistent"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
    }
}
