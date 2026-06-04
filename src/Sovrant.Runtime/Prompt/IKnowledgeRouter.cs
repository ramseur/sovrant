using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Prompt;

/// <summary>
/// Per-turn knowledge selection result — the top-k items from each knowledge kind
/// that the <see cref="IKnowledgeRouter"/> scored as relevant to the current user message.
/// </summary>
public sealed record KnowledgeSelection(
    IReadOnlyList<KnowledgePage> Skills,
    IReadOnlyList<KnowledgePage> Agents,
    IReadOnlyList<KnowledgePage> DocumentTemplates,
    IReadOnlyList<KnowledgePage> ToolGuides)
{
    public bool IsEmpty =>
        Skills.Count == 0 && Agents.Count == 0
        && DocumentTemplates.Count == 0 && ToolGuides.Count == 0;

    public static readonly KnowledgeSelection Empty = new([], [], [], []);
}

/// <summary>
/// Phase 116 D — selects the top-k relevant knowledge items for a given user message
/// using lightweight heuristic scoring (Jaccard keyword overlap, trigger-phrase matching,
/// intent-to-kind affinity, recency bonus). No LLM call; runs entirely in-process
/// against the <see cref="IKnowledgeStore"/> (served from <c>CachedKnowledgeStore</c>).
/// </summary>
public interface IKnowledgeRouter
{
    /// <summary>
    /// Scores all knowledge items and returns the top-k most relevant to
    /// <paramref name="userMessage"/>.
    /// </summary>
    /// <param name="userMessage">The current turn's user input.</param>
    /// <param name="recentSlugs">Slugs used in recent turns — these receive a recency bonus.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<KnowledgeSelection> SelectAsync(
        string userMessage,
        IReadOnlySet<string>? recentSlugs = null,
        CancellationToken ct = default);
}
