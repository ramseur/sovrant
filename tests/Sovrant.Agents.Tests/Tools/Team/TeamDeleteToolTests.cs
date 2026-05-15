using System.Text.Json;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Tools.Team;

namespace Sovrant.Agents.Tests.Tools.Team;

public sealed class TeamDeleteToolTests
{
    [Fact]
    public async Task ExecuteAsync_Deletes_Existing_Member()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeOrchestrationSystem();
        var tool = new TeamDeleteTool(registry, system);

        var member = new TeamMemberInfo
        {
            Id = "abc123",
            Name = "alice",
            Role = AgentRole.General,
            SystemPrompt = "test",
        };
        registry.RegisterMember(member);

        var input = JsonDocument.Parse("""{"member_id":"abc123"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("deleted", result, StringComparison.Ordinal);
        Assert.Null(registry.GetMember("abc123"));
    }

    [Fact]
    public async Task ExecuteAsync_Unknown_Member_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeOrchestrationSystem();
        var tool = new TeamDeleteTool(registry, system);

        var input = JsonDocument.Parse("""{"member_id":"nonexistent"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Missing_MemberId_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeOrchestrationSystem();
        var tool = new TeamDeleteTool(registry, system);

        var input = JsonDocument.Parse("{}").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    private sealed class FakeOrchestrationSystem : IOrchestrationSystem
    {
        public void RegisterAgent(IAgent agent) { }
        public Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default) =>
            Task.FromResult(AgentResult.Ok(task.Id, "ok"));
        public void CancelTask(string taskId) { }
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
