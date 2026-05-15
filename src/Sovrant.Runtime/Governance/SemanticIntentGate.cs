using System.Text.RegularExpressions;
using Sovrant.Api.Routing;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59a — default <see cref="IIntentGate"/> implementation. Wraps the
/// existing <see cref="IntentClassifier"/> from Sovrant.Api and adds:
/// <list type="bullet">
///   <item>RequiresTools logic based on intent class.</item>
///   <item>Clarification flow for ambiguous short inputs (the "test" fix).</item>
/// </list>
/// No LLM call — purely rule-based for speed.
/// </summary>
public sealed partial class SemanticIntentGate : IIntentGate
{
    /// <summary>Minimum message length below which ambiguous inputs trigger clarification.</summary>
    private const int ShortMessageThreshold = 20;

    /// <summary>
    /// Single-word inputs that are commonly ambiguous — they look like tool
    /// keywords but are just as likely conversational or unclear.
    /// </summary>
    private static readonly HashSet<string> AmbiguousShortInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        "test", "run", "check", "build", "clean", "fix", "go",
        "start", "stop", "try", "do", "help", "ok", "yes", "no",
        "done", "next", "again", "more", "show", "open",
    };

    /// <summary>
    /// Intent classes that generally require tool access.
    /// </summary>
    private static readonly HashSet<IntentClass> ToolRequiringIntents =
    [
        // Code tasks
        IntentClass.CodeGeneration,
        IntentClass.CodeEdit,
        IntentClass.CodeReview,
        IntentClass.Refactor,
        IntentClass.Debugging,
        IntentClass.TestGeneration,
        // Document tasks
        IntentClass.DocGeneration,
        IntentClass.SpecDrafting,
        IntentClass.ReportGeneration,
        // File operations
        IntentClass.FileRead,
        IntentClass.FileSearch,
        IntentClass.FileManage,
        // System operations
        IntentClass.ShellExec,
        IntentClass.EnvConfig,
        IntentClass.GitOps,
    ];

    [GeneratedRegex(@"\b(create|write|generate|draft|build|make)\b.{0,30}\b(document|file|artifact|plan|report|spec|markdown|readme|template)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CreationRequestPattern();

    [GeneratedRegex(@"[\\/][\w.]+\.\w{1,5}\b", RegexOptions.Compiled)]
    private static partial Regex FilePathPattern();

    /// <inheritdoc/>
    public Task<IntentGateResult> ClassifyAsync(
        string message,
        int conversationDepth = 0,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Empty or whitespace-only → conversational, no tools.
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(new IntentGateResult(
                Intent: IntentClass.Conversation,
                Confidence: 1.0f,
                RequiresTools: false,
                NeedsClarification: false,
                SuggestedClarification: null));
        }

        var classification = IntentClassifier.Classify(message, conversationDepth);
        var trimmed = message.Trim();
        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var isSingleWord = words.Length == 1;
        var isShort = trimmed.Length < ShortMessageThreshold;

        // ── Clarification check ────────────────────────────────────────
        // Single-word or very short ambiguous inputs with no conversation
        // context should trigger a clarification prompt rather than blindly
        // exposing tools.
        if (isShort && NeedsClarificationCheck(trimmed, isSingleWord, conversationDepth, classification))
        {
            var clarification = BuildClarification(trimmed);
            return Task.FromResult(new IntentGateResult(
                Intent: classification.Intent,
                Confidence: classification.Complexity,
                RequiresTools: false,
                NeedsClarification: true,
                SuggestedClarification: clarification));
        }

        // ── RequiresTools decision ─────────────────────────────────────
        var requiresTools = DetermineRequiresTools(
            classification.Intent, classification.Complexity, message);

        return Task.FromResult(new IntentGateResult(
            Intent: classification.Intent,
            Confidence: classification.Complexity,
            RequiresTools: requiresTools,
            NeedsClarification: false,
            SuggestedClarification: null));
    }

    /// <summary>
    /// Determines whether a short message needs clarification.
    /// </summary>
    private static bool NeedsClarificationCheck(
        string trimmed,
        bool isSingleWord,
        int conversationDepth,
        IntentClassification classification)
    {
        // If the user is deep into a conversation, short messages are
        // likely follow-ups — don't ask for clarification.
        if (conversationDepth > 2)
            return false;

        // Single-word ambiguous inputs always need clarification at conversation start.
        if (isSingleWord && AmbiguousShortInputs.Contains(trimmed))
            return true;

        // Short messages that the intent classifier maps to Conversation
        // with low complexity are likely genuinely conversational — no
        // clarification needed (e.g. "hello", "thanks").
        if (classification.Intent == IntentClass.Conversation)
            return false;

        // Short messages with a file path are likely tool requests.
        if (FilePathPattern().IsMatch(trimmed))
            return false;

        // Other short messages that classified as tool-requiring but with
        // very low complexity might be ambiguous.
        if (classification.Complexity < 0.15f && isSingleWord)
            return true;

        return false;
    }

    /// <summary>
    /// Determines whether the message requires tool exposure.
    /// </summary>
    private static bool DetermineRequiresTools(
        IntentClass intent, float complexity, string message)
    {
        // Messages with file paths are likely tool requests regardless of intent.
        if (FilePathPattern().IsMatch(message))
            return true;

        // Messages that explicitly ask to create/write/generate a document or artifact
        // always need tools, regardless of intent classification or complexity.
        if (CreationRequestPattern().IsMatch(message))
            return true;

        // Conversational never needs tools.
        if (intent == IntentClass.Conversation)
            return false;

        // Explain and Compare are knowledge tasks — no tools unless complex.
        if (intent is IntentClass.Explain or IntentClass.Compare && complexity < 0.3f)
            return false;

        // Planning at low complexity is just discussion.
        if (intent == IntentClass.Planning && complexity < 0.3f)
            return false;

        // Research at low complexity is just Q&A.
        if (intent == IntentClass.Research && complexity < 0.3f)
            return false;

        // All tool-requiring intents (code, docs, files, system ops).
        if (ToolRequiringIntents.Contains(intent))
            return true;

        // Planning, Research, Explain, Compare at higher complexity need tools.
        if (intent is IntentClass.Planning or IntentClass.Research
            or IntentClass.Explain or IntentClass.Compare)
            return true;

        // Legacy intents — map to tools.
#pragma warning disable CS0618
        if (intent is IntentClass.Creative or IntentClass.Analysis or IntentClass.ToolHeavy)
            return true;
        if (intent == IntentClass.SimpleQa)
            return false;
#pragma warning restore CS0618

        return false;
    }

    /// <summary>
    /// Builds a contextual clarification question for the ambiguous input.
    /// </summary>
    private static string BuildClarification(string input)
    {
#pragma warning disable CA1308 // UseToUpperInvariant — switch matching is lowercase by convention
        var lower = input.ToLowerInvariant();
#pragma warning restore CA1308
        return lower switch
        {
            "test" => "Did you mean to run tests, create a test file, or were you just testing the chat?",
            "run" => "What would you like me to run? A command, a script, or tests?",
            "build" => "Would you like me to run a build command, or are you asking about the build process?",
            "fix" => "What would you like me to fix? Please describe the issue.",
            "check" => "What would you like me to check? A file, tests, or something else?",
            "clean" => "Would you like me to clean build artifacts, or something else?",
            "start" => "What would you like me to start? A server, a process, or something else?",
            "stop" => "What would you like me to stop?",
            "show" => "What would you like me to show you?",
            "open" => "What would you like me to open?",
            _ => $"Could you provide more detail about what you'd like me to do with \"{input}\"?",
        };
    }
}
