using Sovrant.Agents.Swarm;
using Sovrant.Agents.Teams;

namespace Sovrant.Agents.Orchestration;

/// <summary>
/// Phase 52 — given a list of teams and a set of tasks, picks the
/// best-fit team member for each task based on template, tool whitelist,
/// and role. Falls back to ephemeral spawning (null member) when no
/// team member matches a task slot.
/// </summary>
public static class EnsembleSelector
{
    /// <summary>
    /// For each task in <paramref name="tasks"/>, returns the best-fit
    /// <see cref="TeamMemberInfo"/> from the combined roster of the
    /// given teams, or <c>null</c> if no member matches.
    /// </summary>
    public static IReadOnlyDictionary<string, TeamMemberInfo?> SelectMembers(
        IReadOnlyList<SwarmTaskNode> tasks,
        IReadOnlyList<string> teamIds,
        ITeamRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(teamIds);
        ArgumentNullException.ThrowIfNull(registry);

        // Build combined roster across all teams
        var roster = new List<TeamMemberInfo>();
        foreach (var teamId in teamIds)
        {
            roster.AddRange(registry.GetTeamMembers(teamId));
        }

        var result = new Dictionary<string, TeamMemberInfo?>(StringComparer.Ordinal);
        var assigned = new HashSet<string>(StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            var best = FindBestMember(task, roster, assigned);
            result[task.Id] = best;
            if (best is not null)
                assigned.Add(best.Id);
        }

        return result;
    }

    private static TeamMemberInfo? FindBestMember(
        SwarmTaskNode task,
        List<TeamMemberInfo> roster,
        HashSet<string> assigned)
    {
        TeamMemberInfo? bestMatch = null;
        var bestScore = -1;

        foreach (var member in roster)
        {
            if (assigned.Contains(member.Id)) continue;

            var score = 0;

            // Template match is the strongest signal
            if (task.AgentTemplate is not null &&
                string.Equals(member.Template, task.AgentTemplate, StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            // Role match (from task description heuristic or template name)
            if (task.AgentTemplate is not null)
            {
                var templateLower = task.AgentTemplate;
                if (templateLower.Contains("review", StringComparison.OrdinalIgnoreCase) &&
                    member.Role == Models.AgentRole.Reviewer)
                    score += 5;
                else if (templateLower.Contains("code", StringComparison.OrdinalIgnoreCase) &&
                    member.Role == Models.AgentRole.Coder)
                    score += 5;
                else if (templateLower.Contains("test", StringComparison.OrdinalIgnoreCase) &&
                    member.Role == Models.AgentRole.Tester)
                    score += 5;
                else if (templateLower.Contains("research", StringComparison.OrdinalIgnoreCase) &&
                    member.Role == Models.AgentRole.Researcher)
                    score += 5;
            }

            // Tool overlap
            if (task.AllowedTools is { Count: > 0 } taskTools &&
                member.AllowedTools is { Count: > 0 } memberTools)
            {
                var overlap = taskTools.Count(t =>
                    memberTools.Any(m => string.Equals(m, t, StringComparison.OrdinalIgnoreCase)));
                score += overlap;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = member;
            }
        }

        return bestMatch;
    }
}
