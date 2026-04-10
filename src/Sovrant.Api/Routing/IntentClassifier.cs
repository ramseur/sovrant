using System.Text.RegularExpressions;

namespace Sovrant.Api.Routing;

/// <summary>Intent classes recognised by the router.</summary>
public enum IntentClass
{
    /// <summary>Simple factual question or lookup.</summary>
    SimpleQa,
    /// <summary>General conversational exchange.</summary>
    Conversation,
    /// <summary>Reviewing or explaining existing code.</summary>
    CodeReview,
    /// <summary>Writing new code from requirements.</summary>
    CodeGeneration,
    /// <summary>Restructuring existing code without changing behaviour.</summary>
    Refactor,
    /// <summary>Multi-step planning or architecture discussion.</summary>
    Planning,
    /// <summary>Creative writing, brainstorming, or ideation.</summary>
    Creative,
    /// <summary>Data analysis, summarisation, or research.</summary>
    Analysis,
    /// <summary>Investigating errors, stack traces, or unexpected behaviour.</summary>
    Debugging,
    /// <summary>Task that will involve many sequential tool calls.</summary>
    ToolHeavy,
}

/// <summary>The result of classifying a user message.</summary>
/// <param name="Intent">The primary intent class.</param>
/// <param name="Complexity">Estimated complexity score (0.0–1.0).</param>
/// <param name="RecommendedTier">The model tier recommended for this classification.</param>
public sealed record IntentClassification(
    IntentClass Intent,
    float Complexity,
    string RecommendedTier);

/// <summary>
/// Rule-based intent classifier. Analyses the last user message in a request
/// to determine the intent class, complexity, and recommended model tier.
/// No LLM call — purely structural and keyword-based analysis.
/// </summary>
public static partial class IntentClassifier
{
    // ── Keyword patterns ────────────────────────────────────────────────

    [GeneratedRegex(@"\b(refactor|restructure|reorgani[sz]e|rename\s+across|extract\s+(method|class|interface)|move\s+to\s+(its\s+own|separate)|split\s+(into|this)|clean\s*up)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RefactorPattern();

    [GeneratedRegex(@"\b(plan|design|architect|strategy|roadmap|how\s+should\s+(we|I)\s+(approach|structure|organis[ez])|step[\s-]by[\s-]step|break\s+(this\s+)?down)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlanningPattern();

    [GeneratedRegex(@"\b(write|create|implement|build|add|generate|make)\s+(a\s+|an\s+|the\s+)?(\w+\s+)?(function|method|class|component|module|endpoint|API|test|script|program|service|handler|middleware|hook|logic|system|feature|route)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CodeGenPattern();

    [GeneratedRegex(@"\b(review|explain|what\s+does\s+this|walk\s+me\s+through|how\s+does\s+this\s+work|understand|read\s+through|code\s+review)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CodeReviewPattern();

    [GeneratedRegex(@"\b(debug|fix|error|bug|crash|exception|stack\s*trace|traceback|segfault|failing|broken|doesn'?t\s+work|not\s+working)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DebuggingPattern();

    [GeneratedRegex(@"\b(analy[sz]e|summari[sz]e|compare|evaluate|assess|research|investigate|data|statistics|metrics|trends|findings)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AnalysisPattern();

    [GeneratedRegex(@"\b(write\s+(a\s+)?(story|poem|essay|blog|article|narrative)|creative|brainstorm|imagine|fiction|draft)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreativePattern();

    [GeneratedRegex(@"\b(what\s+(is|are|was|were)|who\s+(is|are|was)|when\s+(did|was|is)|where\s+(is|are)|how\s+(many|much|long|old)|define|meaning\s+of)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SimpleQaPattern();

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"\b(first|then|next|after\s+that|finally|step\s+\d|1\)|2\)|3\))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MultiStepPattern();

    [GeneratedRegex(@"[\\/][\w.]+\.\w{1,5}\b", RegexOptions.Compiled)]
    private static partial Regex FileRefPattern();

    /// <summary>
    /// Classifies the user's intent from the last user message in the request.
    /// </summary>
    public static IntentClassification Classify(string? userMessage, int conversationDepth = 0)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            return new IntentClassification(IntentClass.Conversation, 0.1f, ModelTier.Fast);

        var text = userMessage;

        // ── Intent detection (ordered by specificity) ───────────────────
        IntentClass intent;
        if (RefactorPattern().IsMatch(text))
            intent = IntentClass.Refactor;
        else if (PlanningPattern().IsMatch(text))
            intent = IntentClass.Planning;
        else if (DebuggingPattern().IsMatch(text))
            intent = IntentClass.Debugging;
        else if (CodeGenPattern().IsMatch(text))
            intent = IntentClass.CodeGeneration;
        else if (CodeReviewPattern().IsMatch(text))
            intent = IntentClass.CodeReview;
        else if (AnalysisPattern().IsMatch(text))
            intent = IntentClass.Analysis;
        else if (CreativePattern().IsMatch(text))
            intent = IntentClass.Creative;
        else if (SimpleQaPattern().IsMatch(text))
            intent = IntentClass.SimpleQa;
        else
            intent = IntentClass.Conversation;

        // ── Complexity scoring ──────────────────────────────────────────
        var complexity = EstimateComplexity(text, conversationDepth);

        // ── Tier mapping ────────────────────────────────────────────────
        var tier = MapTier(intent, complexity, conversationDepth);

        return new IntentClassification(intent, complexity, tier);
    }

    /// <summary>
    /// Estimates task complexity on a 0.0–1.0 scale based on text structure.
    /// </summary>
    internal static float EstimateComplexity(string text, int conversationDepth)
    {
        var score = 0f;

        // Length component (longer = more complex, up to 0.3)
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        score += Math.Min(wordCount / 200f, 0.3f);

        // Code blocks present (+0.15)
        if (CodeBlockPattern().IsMatch(text))
            score += 0.15f;

        // Multi-step indicators (+0.2)
        var multiStepMatches = MultiStepPattern().Count(text);
        score += Math.Min(multiStepMatches * 0.1f, 0.2f);

        // File references (+0.05 per file, up to 0.15)
        var fileRefs = FileRefPattern().Count(text);
        score += Math.Min(fileRefs * 0.05f, 0.15f);

        // Deep conversation bonus (+0.1)
        if (conversationDepth > 5)
            score += 0.1f;

        return Math.Min(score, 1.0f);
    }

    /// <summary>
    /// Maps intent + complexity to a model tier string.
    /// </summary>
    internal static string MapTier(IntentClass intent, float complexity, int conversationDepth)
    {
        return intent switch
        {
            IntentClass.Refactor => ModelTier.High,
            IntentClass.Planning => ModelTier.High,
            IntentClass.Debugging => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,
            IntentClass.CodeGeneration => complexity > 0.7f ? ModelTier.High : ModelTier.Standard,
            IntentClass.CodeReview => complexity > 0.6f ? ModelTier.High : ModelTier.Standard,
            IntentClass.Analysis => complexity > 0.6f ? ModelTier.High : ModelTier.Standard,
            IntentClass.Creative => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,
            IntentClass.ToolHeavy => ModelTier.Standard,
            IntentClass.SimpleQa => ModelTier.Fast,
            IntentClass.Conversation => conversationDepth > 5 ? ModelTier.Standard : ModelTier.Fast,
            _ => ModelTier.Standard,
        };
    }
}

/// <summary>Well-known model tier names used by the intent router.</summary>
public static class ModelTier
{
    /// <summary>Cheapest/fastest models — simple Q&amp;A, conversation.</summary>
    public const string Fast = "fast";
    /// <summary>Mid-range models — code generation, review, analysis.</summary>
    public const string Standard = "standard";
    /// <summary>Most capable models — planning, refactoring, complex reasoning.</summary>
    public const string High = "high";

    /// <summary>All recognised tier names in ascending capability order.</summary>
    public static readonly IReadOnlyList<string> All = [Fast, Standard, High];
}
