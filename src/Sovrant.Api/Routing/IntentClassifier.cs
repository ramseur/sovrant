using System.Text.RegularExpressions;

namespace Sovrant.Api.Routing;

/// <summary>
/// Intent classes recognised by the router. Expanded taxonomy (20 intents)
/// covering code, documents, files, system ops, research, and conversation.
/// </summary>
public enum IntentClass
{
    // ── Code tasks ─────────────────────────────────────────────────────
    /// <summary>Writing new code (functions, classes, modules, endpoints) from requirements.</summary>
    CodeGeneration,
    /// <summary>Modifying existing code in place (add feature, change behavior, update logic).</summary>
    CodeEdit,
    /// <summary>Reviewing, explaining, or critiquing existing code without changing it.</summary>
    CodeReview,
    /// <summary>Restructuring existing code without changing behaviour.</summary>
    Refactor,
    /// <summary>Investigating errors, stack traces, crashes, or unexpected behaviour.</summary>
    Debugging,
    /// <summary>Creating, modifying, or discussing tests specifically.</summary>
    TestGeneration,

    // ── Document tasks ─────────────────────────────────────────────────
    /// <summary>Creating documentation files (README, API docs, guides, doc comments).</summary>
    DocGeneration,
    /// <summary>Drafting specifications, proposals, architecture documents, or technical plans.</summary>
    SpecDrafting,
    /// <summary>Producing analysis reports, summaries, changelogs, or structured output.</summary>
    ReportGeneration,

    // ── File operations ────────────────────────────────────────────────
    /// <summary>Reading, displaying, or inspecting file contents.</summary>
    FileRead,
    /// <summary>Finding files, symbols, or patterns across the codebase.</summary>
    FileSearch,
    /// <summary>Creating, deleting, moving, copying, or renaming files/directories.</summary>
    FileManage,

    // ── System operations ──────────────────────────────────────────────
    /// <summary>Running a shell command, script, or CLI tool.</summary>
    ShellExec,
    /// <summary>Configuring environment, dependencies, tooling, or project settings.</summary>
    EnvConfig,
    /// <summary>Git-specific operations (commit, branch, merge, PR, diff).</summary>
    GitOps,

    // ── Research / learning ────────────────────────────────────────────
    /// <summary>Explaining a concept, technology, pattern, or API without touching code.</summary>
    Explain,
    /// <summary>Comparing technologies, approaches, libraries, or design tradeoffs.</summary>
    Compare,
    /// <summary>Multi-step planning, architecture design, or strategy discussion.</summary>
    Planning,
    /// <summary>Investigating a topic, gathering information, or analyzing data.</summary>
    Research,

    // ── Conversation ───────────────────────────────────────────────────
    /// <summary>Social exchange: greetings, thanks, small talk, acknowledgment.</summary>
    Conversation,

    // ── Legacy (kept for backward compatibility) ───────────────────────
    /// <summary>Simple factual question or lookup. Prefer Explain for new classification.</summary>
    [Obsolete("Use Explain or Compare instead. Kept for backward compatibility.")]
    SimpleQa = 100,
    /// <summary>Creative writing or ideation. Prefer DocGeneration/SpecDrafting for new classification.</summary>
    [Obsolete("Use DocGeneration, SpecDrafting, or ReportGeneration instead.")]
    Creative = 101,
    /// <summary>Data analysis or research. Prefer Research or ReportGeneration for new classification.</summary>
    [Obsolete("Use Research or ReportGeneration instead.")]
    Analysis = 102,
    /// <summary>Multi-tool task. No longer a distinct intent — complexity score handles this.</summary>
    [Obsolete("Complexity score replaces this. Kept for backward compatibility.")]
    ToolHeavy = 103,
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
    // ── Code task patterns ──────────────────────────────────────────────

    [GeneratedRegex(@"\b(refactor|restructure|reorgani[sz]e|rename\s+across|extract\s+(method|class|interface)|move\s+to\s+(its\s+own|separate)|split\s+(into|this)|clean\s*up)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RefactorPattern();

    [GeneratedRegex(@"\b(write|create|implement|build|add|generate|make)\s+(a\s+|an\s+|the\s+)?(\w+\s+)?(function|method|class|component|module|endpoint|API|script|program|service|handler|middleware|hook|logic|system|feature|route|app|application|database|schema|table|model|controller|view|page|form|widget|plugin)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CodeGenPattern();

    [GeneratedRegex(@"\b(change|update|modify|edit|alter|adjust|replace|swap|add\s+to|remove\s+from|set\s+the|increase|decrease|toggle|enable|disable)\s+.{0,40}\b(in|on|at|from|to)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CodeEditPattern();

    [GeneratedRegex(@"\b(review|what\s+does\s+this|walk\s+me\s+through|how\s+does\s+this\s+work|read\s+through|code\s+review|check\s+(this|the)\s+(code|implementation))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CodeReviewPattern();

    [GeneratedRegex(@"\b(debug|fix|error|bug|crash|exception|stack\s*trace|traceback|segfault|failing|broken|doesn'?t\s+work|not\s+working|issue\s+with|problem\s+with)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DebuggingPattern();

    [GeneratedRegex(@"\b(write|create|add|generate|make)\s+(a\s+|an\s+|the\s+)?(unit\s+|integration\s+|e2e\s+|end[\s-]to[\s-]end\s+)?test(s|ing|case)?\b|\btest\s+(this|the|for|coverage)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex TestGenPattern();

    // ── Document task patterns ──────────────────────────────────────────

    [GeneratedRegex(@"\b(write|create|generate|draft|make)\s+(a\s+|an\s+|me\s+a\s+|the\s+)?(README|documentation|API\s+doc\w*|doc\s+comment|JSDoc|XML\s+doc\w*|getting[\s-]started\s+guide|user\s+guide|changelog|release\s+notes)\b|\badd\s+(doc\s+)?comments?\s+to\b|\bdocument\s+(this|the|all|every)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DocGenPattern();

    [GeneratedRegex(@"\b(write|create|draft|generate|make)\s+(a\s+|an\s+|me\s+a\s+|the\s+)?(technical\s+)?(spec|specification|proposal|architecture\s+doc\w*|design\s+doc\w*|RFC|ADR|PRD|requirements\s+doc\w*|business\s+process|system\s+design)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SpecDraftPattern();

    [GeneratedRegex(@"\b(write|create|generate|draft|make|produce)\s+(a\s+|an\s+|me\s+a\s+|the\s+)?(report|summary|audit|analysis\s+doc|overview|digest|brief|document|markdown\s+(file|doc|artifact)|plan)\b|\b(summari[sz]e|report\s+on|create\s+a\s+changelog)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ReportGenPattern();

    // ── File operation patterns ─────────────────────────────────────────

    [GeneratedRegex(@"\b(show|display|print|cat|read|open|look\s+at|inspect|view|what'?s\s+in)\s+(me\s+)?(the\s+)?(contents?\s+(of\s+)?)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FileReadBasePattern();

    [GeneratedRegex(@"\b(find|search|grep|look\s+for|locate|where\s+is|where\s+are|which\s+file|scan\s+for)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FileSearchPattern();

    [GeneratedRegex(@"\b(delete|remove|rename|move|copy|mkdir|create\s+(a\s+)?folder|create\s+(a\s+)?directory|rm\s+|mv\s+|cp\s+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FileManagePattern();

    // ── System operation patterns ───────────────────────────────────────

    [GeneratedRegex(@"\b(run|execute|launch|start)\s+(the\s+)?(command|script|build|server|process|migration|npm|yarn|pip|dotnet|cargo|go|make|gradle|mvn|docker|kubectl|terraform|ansible)\b|\b(npm\s+(install|run|test)|yarn\s+(add|run)|pip\s+install|dotnet\s+(build|run|test|publish)|cargo\s+(build|run|test)|go\s+(build|run|test))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ShellExecPattern();

    [GeneratedRegex(@"\b(set\s*up|configure|install|init(iali[sz]e)?|bootstrap|scaffold|add\s+(a\s+)?(package|dependency|nuget|npm|crate|gem|library))\b|\b(update\s+(the\s+)?(config|configuration|settings|CI|pipeline|dockerfile|docker[\s-]compose|makefile|package\.json|\.env|tsconfig|eslint|prettier))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EnvConfigPattern();

    [GeneratedRegex(@"\b(commit|push|pull|merge|rebase|cherry[\s-]pick|stash|branch|checkout|diff|log|blame|tag|release|PR|pull\s+request|git\s+\w+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex GitOpsPattern();

    // ── Research / learning patterns ────────────────────────────────────

    [GeneratedRegex(@"\b(explain|teach\s+me|how\s+does|how\s+do\s+I|how\s+many|what\s+(is|are)|what'?s\s+(the|a)\s+\w+|who\s+(is|was|are)|tell\s+me\s+about|describe|clarify|help\s+me\s+understand|ELI5|in\s+simple\s+terms)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ExplainPattern();

    [GeneratedRegex(@"\b(compare|versus|vs\.?|difference\s+between|pros\s+and\s+cons|which\s+(is|should\s+I)|should\s+I\s+use|better\s+(than|choice)|trade[\s-]?offs?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ComparePattern();

    [GeneratedRegex(@"\b(plan|design|architect|strategy|roadmap|how\s+should\s+(we|I)\s+(approach|structure|organis[ez])|step[\s-]by[\s-]step|break\s+(this\s+)?down|scope\s+out)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlanningPattern();

    [GeneratedRegex(@"\b(research|investigate|look\s+into|dig\s+into|find\s+out|explore|what\s+are\s+the\s+best\s+practices|state\s+of\s+the\s+art|current\s+approaches)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ResearchPattern();

    // ── Structural patterns (used for complexity scoring) ─────────────��─

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
        // More specific patterns are checked first to avoid broad patterns
        // swallowing narrow cases (e.g. "write a spec" → SpecDrafting, not CodeGen).
        IntentClass intent;

        // Git operations (very specific keywords)
        if (GitOpsPattern().IsMatch(text))
            intent = IntentClass.GitOps;
        // Refactoring (specific restructuring language)
        else if (RefactorPattern().IsMatch(text))
            intent = IntentClass.Refactor;
        // Test generation (before CodeGen — "write tests" should not match CodeGen)
        else if (TestGenPattern().IsMatch(text))
            intent = IntentClass.TestGeneration;
        // Spec/architecture documents (before DocGen and ReportGen — more specific)
        else if (SpecDraftPattern().IsMatch(text))
            intent = IntentClass.SpecDrafting;
        // Documentation (before ReportGen — README/API docs are more specific)
        else if (DocGenPattern().IsMatch(text))
            intent = IntentClass.DocGeneration;
        // Reports/summaries/markdown artifacts (before CodeGen)
        else if (ReportGenPattern().IsMatch(text))
            intent = IntentClass.ReportGeneration;
        // Research (before Debugging — "research best practices for error handling"
        // should not hit Debugging just because "error" is present)
        else if (ResearchPattern().IsMatch(text))
            intent = IntentClass.Research;
        // Debugging (specific error/bug language)
        else if (DebuggingPattern().IsMatch(text))
            intent = IntentClass.Debugging;
        // Code generation (broad "create/build/make" + code artifact)
        else if (CodeGenPattern().IsMatch(text))
            intent = IntentClass.CodeGeneration;
        // Code edit (modify existing code in place)
        else if (CodeEditPattern().IsMatch(text))
            intent = IntentClass.CodeEdit;
        // Shell execution (run/execute commands)
        else if (ShellExecPattern().IsMatch(text))
            intent = IntentClass.ShellExec;
        // Environment/config setup
        else if (EnvConfigPattern().IsMatch(text))
            intent = IntentClass.EnvConfig;
        // File management (delete, move, rename)
        else if (FileManagePattern().IsMatch(text))
            intent = IntentClass.FileManage;
        // File search (find, grep, locate)
        else if (FileSearchPattern().IsMatch(text))
            intent = IntentClass.FileSearch;
        // File read (show, cat, read) — must have a file reference nearby
        else if (FileReadBasePattern().IsMatch(text) && FileRefPattern().IsMatch(text))
            intent = IntentClass.FileRead;
        // Compare (vs, pros and cons, which should I use)
        else if (ComparePattern().IsMatch(text))
            intent = IntentClass.Compare;
        // Explain (what is X, teach me, ELI5) — before Planning to avoid
        // "teach me about design patterns" hitting Planning's "design" keyword
        else if (ExplainPattern().IsMatch(text))
            intent = IntentClass.Explain;
        // Planning (architecture, strategy, roadmap)
        else if (PlanningPattern().IsMatch(text))
            intent = IntentClass.Planning;
        // Code review (review, explain code, walk me through)
        else if (CodeReviewPattern().IsMatch(text))
            intent = IntentClass.CodeReview;
        // Default: conversation
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
            // Code tasks
            IntentClass.Refactor => ModelTier.High,
            IntentClass.Debugging => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,
            IntentClass.CodeGeneration => complexity > 0.7f ? ModelTier.High : ModelTier.Standard,
            IntentClass.CodeEdit => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,
            IntentClass.CodeReview => complexity > 0.6f ? ModelTier.High : ModelTier.Standard,
            IntentClass.TestGeneration => complexity > 0.6f ? ModelTier.High : ModelTier.Standard,

            // Document tasks
            IntentClass.SpecDrafting => ModelTier.High,
            IntentClass.DocGeneration => ModelTier.Standard,
            IntentClass.ReportGeneration => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,

            // File operations — fast, low complexity
            IntentClass.FileRead => ModelTier.Fast,
            IntentClass.FileSearch => ModelTier.Fast,
            IntentClass.FileManage => ModelTier.Fast,

            // System operations
            IntentClass.ShellExec => ModelTier.Fast,
            IntentClass.EnvConfig => ModelTier.Standard,
            IntentClass.GitOps => ModelTier.Fast,

            // Research / learning
            IntentClass.Explain => ModelTier.Standard,
            IntentClass.Compare => ModelTier.Standard,
            IntentClass.Planning => ModelTier.High,
            IntentClass.Research => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,

            // Conversation
            IntentClass.Conversation => conversationDepth > 5 ? ModelTier.Standard : ModelTier.Fast,

            // Legacy intents (backward compatibility)
#pragma warning disable CS0618 // Obsolete
            IntentClass.SimpleQa => ModelTier.Fast,
            IntentClass.Creative => complexity > 0.5f ? ModelTier.High : ModelTier.Standard,
            IntentClass.Analysis => complexity > 0.6f ? ModelTier.High : ModelTier.Standard,
            IntentClass.ToolHeavy => ModelTier.Standard,
#pragma warning restore CS0618

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
