using Sovrant.Agents.Models;
using Sovrant.Agents.Teams;

namespace Sovrant.Agents.Tests.Teams;

public sealed class InMemoryTeamRegistryTests
{
    private static TeamMemberInfo MakeMember(string? id = null, string? name = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString("N"),
        Name = name ?? "test-member",
        Role = AgentRole.General,
        SystemPrompt = "test prompt",
    };

    [Fact]
    public void RegisterMember_Returns_Id()
    {
        var registry = new InMemoryTeamRegistry();
        var member = MakeMember(id: "abc123");

        var id = registry.RegisterMember(member);

        Assert.Equal("abc123", id);
    }

    [Fact]
    public void GetMember_Returns_Registered_Member()
    {
        var registry = new InMemoryTeamRegistry();
        var member = MakeMember(id: "abc123", name: "alice");
        registry.RegisterMember(member);

        var retrieved = registry.GetMember("abc123");

        Assert.NotNull(retrieved);
        Assert.Equal("alice", retrieved.Name);
    }

    [Fact]
    public void GetMember_Unknown_Returns_Null()
    {
        var registry = new InMemoryTeamRegistry();
        Assert.Null(registry.GetMember("nonexistent"));
    }

    [Fact]
    public void RemoveMember_Returns_True_For_Existing()
    {
        var registry = new InMemoryTeamRegistry();
        registry.RegisterMember(MakeMember(id: "abc"));

        Assert.True(registry.RemoveMember("abc"));
        Assert.Null(registry.GetMember("abc"));
    }

    [Fact]
    public void RemoveMember_Returns_False_For_Unknown()
    {
        var registry = new InMemoryTeamRegistry();
        Assert.False(registry.RemoveMember("nonexistent"));
    }

    [Fact]
    public void GetAllMembers_Returns_All()
    {
        var registry = new InMemoryTeamRegistry();
        registry.RegisterMember(MakeMember(id: "a", name: "alice"));
        registry.RegisterMember(MakeMember(id: "b", name: "bob"));

        var all = registry.GetAllMembers();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAllMembers_Empty_Returns_Empty()
    {
        var registry = new InMemoryTeamRegistry();
        Assert.Empty(registry.GetAllMembers());
    }
}
