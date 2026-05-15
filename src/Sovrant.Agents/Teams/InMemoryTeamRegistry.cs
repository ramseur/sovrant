using System.Collections.Concurrent;

namespace Sovrant.Agents.Teams;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ITeamRegistry"/>.
/// </summary>
public sealed class InMemoryTeamRegistry : ITeamRegistry
{
    private readonly ConcurrentDictionary<string, TeamMemberInfo> _members =
        new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string RegisterMember(TeamMemberInfo member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _members[member.Id] = member;
        return member.Id;
    }

    /// <inheritdoc/>
    public bool RemoveMember(string memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        return _members.TryRemove(memberId, out _);
    }

    /// <inheritdoc/>
    public TeamMemberInfo? GetMember(string memberId)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        return _members.TryGetValue(memberId, out var member) ? member : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TeamMemberInfo> GetAllMembers() =>
        _members.Values.ToList();
}
