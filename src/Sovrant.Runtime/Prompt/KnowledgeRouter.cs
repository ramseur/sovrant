using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Prompt;

/// <summary>
/// Phase 116 D — default <see cref="IKnowledgeRouter"/> implementation.
/// Scores knowledge items per turn using four signals:
/// <list type="number">
///   <item>Trigger match — if a skill's trigger phrase appears verbatim in the input, score 1.5 (always selected).</item>
///   <item>Jaccard keyword overlap — intersection/union of lowercased word sets between input and item text.</item>
///   <item>Intent-to-kind affinity — code intents favour skills; document intents favour templates, etc.</item>
///   <item>Recency bonus — items used in the last few turns score +0.3.</item>
/// </list>
/// All signals are computed in-process against the <see cref="IKnowledgeStore"/> cache
/// (served by <c>CachedKnowledgeStore</c>); no extra LLM call is made.
/// </summary>
internal sealed class KnowledgeRouter : IKnowledgeRouter
{
    private readonly IKnowledgeStore _store;

    private const int MaxSkills = 5;
    private const int MaxAgents = 3;
    private const int MaxDocTemplates = 3;
    private const int MaxToolGuides = 3;

    private const float MinScore = 0.05f;
    private const float TriggerScore = 1.5f;
    // MCP tool selection: return all tools if fewer than this score above MinScore.
    private const int MinMcpToolsBeforeFallback = 3;
    // Hard cap on MCP tools sent per turn to limit token pressure.
    private const int MaxMcpToolsPerTurn = 20;
    private const float JaccardWeight = 0.6f;
    private const float IntentAffinityMax = 0.4f;
    private const float RecencyBonus = 0.3f;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her",
        "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its",
        "let", "man", "new", "now", "old", "see", "two", "way", "who", "did", "she",
        "use", "far", "off", "yet", "try", "per", "via", "etc",
    };

    public KnowledgeRouter(IKnowledgeStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeSelection> SelectAsync(
        string userMessage,
        IReadOnlySet<string>? recentSlugs = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return KnowledgeSelection.Empty;

        var intent = IntentClassifier.Classify(userMessage).Intent;
#pragma warning disable CA1308 // ToLowerInvariant — case-fold for similarity scoring, not identifier normalisation
        var inputTokens = Tokenize(userMessage.ToLowerInvariant());
        var inputLower = userMessage.ToLowerInvariant();
#pragma warning restore CA1308
        var projectId = KnowledgeScope.ProjectIdFor(Directory.GetCurrentDirectory());

        // Issue all four DB calls in parallel; CachedKnowledgeStore serves from in-memory cache after the first call.
        var results = await Task.WhenAll(
            _store.GetAllEffectiveAsync("skills", projectId, ct),
            _store.GetAllEffectiveAsync("agents", projectId, ct),
            _store.GetAllEffectiveAsync("document-templates", projectId, ct),
            _store.GetAllEffectiveAsync("tools", projectId, ct)).ConfigureAwait(false);

        return new KnowledgeSelection(
            ScoreAndSelect(results[0], inputTokens, inputLower, intent, recentSlugs, "skills", MaxSkills),
            ScoreAndSelect(results[1], inputTokens, inputLower, intent, recentSlugs, "agents", MaxAgents),
            ScoreAndSelect(results[2], inputTokens, inputLower, intent, recentSlugs, "document-templates", MaxDocTemplates),
            ScoreAndSelect(results[3], inputTokens, inputLower, intent, recentSlugs, "tools", MaxToolGuides));
    }

    private static List<KnowledgePage> ScoreAndSelect(
        IReadOnlyList<KnowledgePage> pages,
        HashSet<string> inputTokens,
        string inputLower,
        IntentClass intent,
        IReadOnlySet<string>? recentSlugs,
        string kind,
        int topK)
    {
        if (pages.Count == 0) return [];

        var scored = new List<(KnowledgePage Page, float Score)>(pages.Count);
        foreach (var page in pages)
        {
            var score = ScoreItem(page, inputTokens, inputLower, intent, kind, recentSlugs);
            if (score >= MinScore)
                scored.Add((page, score));
        }

        if (scored.Count == 0) return [];

        scored.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        return scored.Take(topK).Select(static x => x.Page).ToList();
    }

    private static float ScoreItem(
        KnowledgePage page,
        HashSet<string> inputTokens,
        string inputLower,
        IntentClass intent,
        string kind,
        IReadOnlySet<string>? recentSlugs)
    {
        // Trigger match — if the trigger phrase appears verbatim in the input, guarantee selection.
        if (!string.IsNullOrWhiteSpace(page.Trigger))
        {
#pragma warning disable CA1308
            var trigger = page.Trigger.Trim().ToLowerInvariant();
#pragma warning restore CA1308
            if (trigger.Length > 0 && inputLower.Contains(trigger, StringComparison.Ordinal))
                return TriggerScore;
        }

        // Jaccard overlap on name + description + trigger word set.
#pragma warning disable CA1308
        var itemTokens = Tokenize($"{page.Name} {page.Description} {page.Trigger}".ToLowerInvariant());
#pragma warning restore CA1308
        var jaccard = Jaccard(inputTokens, itemTokens);

        var affinity = IntentKindAffinity(intent, kind);
        var recency = recentSlugs?.Contains(page.Slug) == true ? RecencyBonus : 0f;

        return Math.Min(1.0f, jaccard * JaccardWeight + affinity + recency);
    }

    private static float IntentKindAffinity(IntentClass intent, string kind) =>
        (intent, kind) switch
        {
            // Code intents boost skills and tool guides.
            (IntentClass.CodeGeneration or IntentClass.CodeEdit or IntentClass.Refactor
             or IntentClass.Debugging or IntentClass.TestGeneration or IntentClass.CodeReview, "skills")
                => IntentAffinityMax,
            (IntentClass.CodeGeneration or IntentClass.CodeEdit or IntentClass.Refactor
             or IntentClass.Debugging or IntentClass.TestGeneration, "tools")
                => IntentAffinityMax * 0.75f,
            // Document intents boost templates and skills.
            (IntentClass.DocGeneration or IntentClass.SpecDrafting or IntentClass.ReportGeneration,
             "document-templates")
                => IntentAffinityMax,
            (IntentClass.DocGeneration or IntentClass.SpecDrafting, "skills")
                => IntentAffinityMax * 0.5f,
            // Research and planning boost agents.
            (IntentClass.Research or IntentClass.Planning, "agents")
                => IntentAffinityMax * 0.75f,
            // Shell/git/env boost tool guides.
            (IntentClass.ShellExec or IntentClass.GitOps or IntentClass.EnvConfig, "tools")
                => IntentAffinityMax * 0.75f,
            // File ops boost tool guides moderately.
            (IntentClass.FileRead or IntentClass.FileSearch or IntentClass.FileManage, "tools")
                => IntentAffinityMax * 0.5f,
            _ => 0f,
        };

    private static float Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0f;
        var intersection = 0;
        foreach (var token in a)
        {
            if (b.Contains(token)) intersection++;
        }
        if (intersection == 0) return 0f;
        var union = a.Count + b.Count - intersection;
        return (float)intersection / union;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ToolDefinition> SelectMcpTools(
        string userMessage,
        IReadOnlyList<ToolDefinition> mcpTools)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || mcpTools.Count == 0)
            return mcpTools;

#pragma warning disable CA1308
        var inputTokens = Tokenize(userMessage.ToLowerInvariant());
#pragma warning restore CA1308

        var scored = new List<(ToolDefinition Tool, float Score)>(mcpTools.Count);
        foreach (var tool in mcpTools)
        {
#pragma warning disable CA1308
            var itemTokens = Tokenize($"{tool.Name} {tool.Description}".ToLowerInvariant());
#pragma warning restore CA1308
            var score = Jaccard(inputTokens, itemTokens);
            if (score >= MinScore)
                scored.Add((tool, score));
        }

        // Safety fallback: if scoring finds very few relevant tools, keep all so the
        // LLM retains full access rather than losing potentially useful capabilities.
        if (scored.Count < MinMcpToolsBeforeFallback)
            return mcpTools;

        scored.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        return scored.Take(MaxMcpToolsPerTurn).Select(static x => x.Tool).ToList();
    }

    // Input must already be lowercased by the caller.
    private static HashSet<string> Tokenize(string lower)
    {
        if (string.IsNullOrWhiteSpace(lower)) return [];
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var start = 0;
        for (var i = 0; i <= lower.Length; i++)
        {
            var isSep = i == lower.Length || IsWordSeparator(lower[i]);
            if (isSep && i > start)
            {
                var word = lower.AsSpan(start, i - start);
                if (word.Length >= 3 && !StopWords.Contains(word.ToString()))
                    tokens.Add(word.ToString());
            }
            if (isSep) start = i + 1;
        }
        return tokens;
    }

    private static bool IsWordSeparator(char c) =>
        c is ' ' or '\t' or '\n' or '\r' or '.' or ',' or '!' or '?' or ';' or ':'
            or '-' or '_' or '/' or '\\' or '(' or ')' or '[' or ']' or '{' or '}' or '"' or '\'';
}
