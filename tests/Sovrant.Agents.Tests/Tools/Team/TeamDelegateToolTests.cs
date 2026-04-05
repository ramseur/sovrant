using System.Text.Json;
using Sovrant.Agents.Abstractions;
using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;
using Sovrant.Tools.Team;

namespace Sovrant.Agents.Tests.Tools.Team;

public sealed class TeamDelegateToolTests
{
    private static IAgent FakeFactory(TeamMemberInfo member) => new FakeAgent(member.Name);

    [Fact]
    public async Task ExecuteAsync_Unknown_Member_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeMultiAgentSystem();
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        var input = JsonDocument.Parse("""{"member_id":"nonexistent","prompt":"test"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
        Assert.Contains("no team member", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Missing_MemberId_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeMultiAgentSystem();
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        var input = JsonDocument.Parse("""{"prompt":"test"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Missing_Prompt_Returns_Error()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeMultiAgentSystem();
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        registry.RegisterMember(new TeamMemberInfo
        {
            Id = "abc", Name = "alice", Role = AgentRole.General, SystemPrompt = "test",
        });

        var input = JsonDocument.Parse("""{"member_id":"abc"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("Error", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_Successful_Delegation_Updates_Status()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeMultiAgentSystem { Output = "Agent says hello." };
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        var member = new TeamMemberInfo
        {
            Id = "abc", Name = "alice", Role = AgentRole.General, SystemPrompt = "test",
        };
        registry.RegisterMember(member);

        var input = JsonDocument.Parse("""{"member_id":"abc","prompt":"say hello"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Equal("Agent says hello.", result);
        Assert.Equal(TeamMemberStatus.Completed, member.Status);
        Assert.Equal("Agent says hello.", member.LastOutput);
    }

    [Fact]
    public async Task ExecuteAsync_Failed_Delegation_Updates_Status()
    {
        var registry = new InMemoryTeamRegistry();
        var system = new FakeMultiAgentSystem { Error = "Provider crashed" };
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        var member = new TeamMemberInfo
        {
            Id = "abc", Name = "bob", Role = AgentRole.General, SystemPrompt = "test",
        };
        registry.RegisterMember(member);

        var input = JsonDocument.Parse("""{"member_id":"abc","prompt":"crash"}""").RootElement;
        var result = await tool.ExecuteAsync(input);

        Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TeamMemberStatus.Failed, member.Status);
        Assert.Equal("Provider crashed", member.LastError);
    }

    [Fact]
    public async Task ExecuteAsync_Registers_Agent_Only_Once()
    {
        var registry = new InMemoryTeamRegistry();
        var registerCount = 0;
        var system = new FakeMultiAgentSystem { Output = "ok", OnRegister = () => registerCount++ };
        var tool = new TeamDelegateTool(registry, system, FakeFactory);

        var member = new TeamMemberInfo
        {
            Id = "abc", Name = "alice", Role = AgentRole.General, SystemPrompt = "test",
        };
        registry.RegisterMember(member);

        var input = JsonDocument.Parse("""{"member_id":"abc","prompt":"first"}""").RootElement;
        await tool.ExecuteAsync(input);
        await tool.ExecuteAsync(input);

        Assert.Equal(1, registerCount);
    }

    private sealed class FakeMultiAgentSystem : IMultiAgentSystem
    {
        public string? Output { get; init; }
        public string? Error { get; init; }
        public Action? OnRegister { get; init; }

        public void RegisterAgent(IAgent agent) => OnRegister?.Invoke();

        public Task<AgentResult> RunTaskAsync(AgentTask task, CancellationToken ct = default)
        {
            var result = Error is not null
                ? AgentResult.Fail(task.Id, Error)
                : AgentResult.Ok(task.Id, Output ?? string.Empty);
            return Task.FromResult(result);
        }

        public void CancelTask(string taskId) { }
        public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeAgent : IAgent
    {
        public string Name { get; }
        public FakeAgent(string name) => Name = name;
        public Task<AgentResult> HandleAsync(AgentTask task, CancellationToken ct = default) =>
            Task.FromResult(AgentResult.Ok(task.Id, "fake"));
    }
}
