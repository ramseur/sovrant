using System.Diagnostics;
using System.Security.Cryptography;
using Sovrant.Web.Adapters;

namespace Sovrant.Web.Components.Shared;

public sealed class ChatMessageModel
{
    private readonly Stopwatch _stopwatch = new();
    private static readonly string[] ThinkingPhrases =
    [
        "Thinking...", "Reasoning through it...", "Gathering thoughts...",
        "Connecting the dots...", "Weighing the options...", "Working on it...",
    ];

    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;
    public bool IsUser => Role == "user";
    public bool IsThinking { get; set; }
    public string ThinkingText { get; set; } = string.Empty;
    public bool IsStreaming { get; set; }
    public bool IsComplete { get; set; }
    public bool HasError { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsExecutingTools { get; set; }
    public string ExecutionStatusText { get; set; } = string.Empty;
    public string ElapsedText { get; set; } = string.Empty;
    private int _completedToolCount;
    public List<ToolUseModel> ToolUses { get; } = [];

    /// <summary>Artifacts auto-saved from large text blocks (not tied to a specific tool use row).</summary>
    public List<AutoArtifactModel> StandaloneArtifacts { get; } = [];

    /// <summary>The model that generated this response.</summary>
    public string? ModelName { get; set; }

    /// <summary>The provider that served this response.</summary>
    public string? ProviderName { get; set; }

    /// <summary>Display name of the user who sent this message (local part of email). Never a raw ID.</summary>
    public string? UserDisplayName { get; set; }

    /// <summary>
    /// Display label for the message sender. Shows "Provider · model" for assistant
    /// messages once the turn completes, falls back to "Sovrant" while streaming.
    /// </summary>
    public string SenderLabel
    {
        get
        {
            if (Role == "user") return string.IsNullOrEmpty(UserDisplayName) ? "You" : UserDisplayName;
            if (ProviderName is not null && ModelName is not null)
                return $"{ProviderName} · {FormatModelName(ModelName)}";
            if (ModelName is not null)
                return FormatModelName(ModelName);
            return "Sovrant";
        }
    }

    private static string FormatModelName(string model)
    {
        var slash = model.LastIndexOf('/');
        return slash >= 0 ? model[(slash + 1)..] : model;
    }

    // ── Phase 59d — intent narration ───────────────────────────────────

    /// <summary>What the system thinks the user wants, e.g. "I'll create a PDF report for you". Set when IntentNarrated fires.</summary>
    public string? IntentNarration { get; set; }

    /// <summary>Summary of what was actually done, derived from ToolUses after completion.</summary>
    public string? ActionSummary { get; set; }

    // ── Phase 59 properties ─────────────────────────────────────────────

    /// <summary>Phase 59a — clarification question from the intent gate.</summary>
    public string? ClarificationQuestion { get; set; }

    /// <summary>Phase 59b — formatted plan text awaiting approval.</summary>
    public string? PlanContent { get; set; }

    /// <summary>Phase 59b — ID of the presented plan.</summary>
    public string? PlanId { get; set; }

    /// <summary>Phase 59b — whether the presented plan requires user approval.</summary>
    public bool PlanRequiresApproval { get; set; }

    /// <summary>Phase 59e — current step index (1-based).</summary>
    public int CurrentStep { get; set; }

    /// <summary>Phase 59e — total number of steps in the plan.</summary>
    public int TotalSteps { get; set; }

    /// <summary>Phase 59e — human-readable step progress summary.</summary>
    public string? StepProgressText { get; set; }

    public void StartThinking(string? prompt = null)
    {
        IsThinking = true;
        ThinkingText = PickThinkingPhrase(prompt);
        _stopwatch.Restart();
        ElapsedText = "0s";
    }

    private static string PickThinkingPhrase(string? prompt)
    {
        if (!string.IsNullOrEmpty(prompt))
        {
            var p = prompt;
            if (ContainsAny(p, "pdf", "document", "word", "excel", "spreadsheet", "powerpoint", "report"))
                return "Preparing your document...";
            if (ContainsAny(p, "create", "write", "generate", "build", "make") &&
                ContainsAny(p, "code", "script", "function", "class", "program", "app"))
                return "Writing the code...";
            if (ContainsAny(p, "search", "find", "look up", "lookup", "google", "web"))
                return "Searching the web...";
            if (ContainsAny(p, "analyze", "analyse", "review", "check", "audit", "read"))
                return "Reading and analyzing...";
            if (ContainsAny(p, "fix", "debug", "error", "bug", "issue", "problem"))
                return "Investigating the issue...";
            if (ContainsAny(p, "explain", "what", "how", "why", "describe", "tell me"))
                return "Looking that up...";
            if (ContainsAny(p, "summarize", "summarise", "summary", "recap"))
                return "Summarizing...";
            if (ContainsAny(p, "translate", "translation", "convert"))
                return "Translating...";
            if (ContainsAny(p, "create", "make", "generate", "build", "write"))
                return "Working on your request...";
        }
        var idx = RandomNumberGenerator.GetInt32(ThinkingPhrases.Length);
        return ThinkingPhrases[idx];
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

    /// <summary>Call periodically (e.g. every second) to refresh the elapsed display.</summary>
    public void UpdateElapsed()
    {
        var elapsed = _stopwatch.Elapsed;
        ElapsedText = elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
            : $"{(int)elapsed.TotalSeconds}s";
    }

    public void StopThinking() => IsThinking = false;

    public void AppendText(string chunk)
    {
        if (IsThinking) StopThinking();
        if (!IsStreaming) { IsStreaming = true; IsComplete = false; }
        Text += chunk;
    }

    public void AddToolUse(string toolName, string toolUseId)
    {
        if (IsThinking) StopThinking();
        IsExecutingTools = true;
        ExecutionStatusText = FriendlyToolStatus(toolName);
        ToolUses.Add(new ToolUseModel { ToolName = toolName, ToolUseId = toolUseId, Status = "Running..." });
    }

    public static string FriendlyToolLabel(string toolName) => toolName switch
    {
        "DocumentGenerate" => "Generate Document",
        "Artifact" => "Save File",
        "Bash" => "Run Command",
        "Read" => "Read File",
        "Write" => "Write File",
        "Edit" => "Edit File",
        "Glob" => "Find Files",
        "Grep" => "Search Files",
        "WebSearch" => "Search Web",
        "WebFetch" => "Fetch URL",
        "Agent" => "Sub-Agent",
        "Swarm" => "Coordinate Swarm",
        "TeamCreate" => "Create Team",
        "TeamRun" => "Run Team",
        "TeamDelegate" => "Delegate to Team",
        "Mission" => "Mission",
        _ => toolName,
    };

    private static string FriendlyToolStatus(string toolName) => toolName switch
    {
        "DocumentGenerate" => "Creating your document...",
        "Artifact" => "Saving file...",
        "Bash" => "Running command...",
        "Read" => "Reading file...",
        "Write" => "Writing file...",
        "Edit" => "Editing file...",
        "Glob" => "Finding files...",
        "Grep" => "Searching files...",
        "WebSearch" => "Searching the web...",
        "WebFetch" => "Fetching page...",
        "Agent" => "Working with sub-agent...",
        "Swarm" => "Coordinating swarm...",
        "TeamCreate" => "Creating team...",
        "TeamRun" => "Running team...",
        "TeamDelegate" => "Delegating task...",
        "Mission" => "Running mission...",
        _ => $"Running {toolName}...",
    };

    public void UpdateToolResult(string toolUseId, string content, bool isError)
    {
        var tu = ToolUses.FirstOrDefault(u => u.ToolUseId == toolUseId);
        if (tu is not null)
        {
            tu.Result = content;
            tu.IsError = isError;
            tu.Status = isError ? "Error" : "Done";
            _completedToolCount++;
            ExecutionStatusText = $"Completed {_completedToolCount}/{ToolUses.Count} tool calls";
        }
    }

    public void CompleteStreaming()
    {
        IsThinking = false;
        IsStreaming = false;
        IsComplete = true;
        IsExecutingTools = false;
        _stopwatch.Stop();
        UpdateElapsed();
        ActionSummary = BuildActionSummary();
    }

    private string? BuildActionSummary()
    {
        if (ToolUses.Count == 0) return null;
        var parts = new List<string>();
        var groups = ToolUses
            .Where(t => !t.IsError)
            .GroupBy(t => t.ToolName)
            .Select(g => (Label: g.Key, Count: g.Count()));
        foreach (var (label, count) in groups)
            parts.Add(count > 1 ? $"{label} ×{count}" : label);
        return parts.Count > 0 ? string.Join(" · ", parts) : null;
    }

    public void SetError(string rawError)
    {
        StopThinking();
        _stopwatch.Stop();
        UpdateElapsed();
        IsStreaming = false;
        HasError = true;
        ErrorMessage = FriendlyError(rawError);
        if (!string.IsNullOrEmpty(Text)) IsComplete = true;
    }

    private static string FriendlyError(string raw)
    {
        // Extract provider/model context prefix if present (e.g. "[OpenRouter · gemma-4:free] ...")
        var context = ExtractProviderContext(raw);
        var prefix = context is not null ? $"{context}: " : "";

        // No provider or model configured — most common first-run issue
        if (raw.Contains("No provider available", StringComparison.OrdinalIgnoreCase))
            return "No provider configured. Go to Settings → Providers and add an API key to get started.";

        // Credits / billing exhausted
        if (raw.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("out of credits", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("credit balance", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("402", StringComparison.Ordinal))
            return $"{prefix}API credits exhausted. Top up your account at the provider's website, or switch to a different provider in Settings.";

        // Rate limited
        if (raw.Contains("429", StringComparison.Ordinal) ||
            raw.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Rate limited by the provider. Wait a moment and try again, or switch to a different model in Settings.";

        // Authentication
        if (raw.Contains("401", StringComparison.Ordinal) ||
            raw.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Authentication failed. Check your API key in Settings → Providers.";

        // Access denied
        if (raw.Contains("403", StringComparison.Ordinal) ||
            raw.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Access denied. Your API key may not have permission for this model.";

        // Model not found
        if (raw.Contains("404", StringComparison.Ordinal) &&
            (raw.Contains("model", StringComparison.OrdinalIgnoreCase) || raw.Contains("not found", StringComparison.OrdinalIgnoreCase)))
            return $"{prefix}Model not found. It may have been removed or renamed — try selecting a different model in Settings.";

        // Context length exceeded
        if (raw.Contains("context_length", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("context length", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("maximum context", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("413", StringComparison.Ordinal))
            return $"{prefix}The conversation is too long for this model. Start a new chat or switch to a model with a larger context window.";

        // Content filtered / safety block
        if (raw.Contains("content_filter", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("content filter", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("moderated", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The request was blocked by the provider's content filter. Try rephrasing.";

        // Bad request
        if (raw.Contains("400", StringComparison.Ordinal))
            return $"{prefix}The provider rejected this request (400). Try starting a new chat or switching models.";

        // Provider-level error wrapper
        if (raw.Contains("Provider returned error", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The provider returned an error. Try again or switch to a different model in Settings.";

        // Service overloaded / unavailable
        if (raw.Contains("529", StringComparison.Ordinal) ||
            raw.Contains("503", StringComparison.Ordinal) ||
            raw.Contains("502", StringComparison.Ordinal) ||
            raw.Contains("overloaded", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The provider is temporarily overloaded. Try again in a moment.";

        if (raw.Contains("500", StringComparison.Ordinal))
            return $"{prefix}The provider hit an internal error (500). Try again.";

        // Connection errors
        if (raw.Contains("connection", StringComparison.OrdinalIgnoreCase) && raw.Contains("refused", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Could not connect to the provider. Check your internet connection and the base URL in Settings.";
        if (raw.Contains("No such host", StringComparison.OrdinalIgnoreCase) ||
            raw.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Could not reach the provider — check your internet connection.";

        // Timeout
        if (raw.Contains("timeout", StringComparison.OrdinalIgnoreCase) || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The request timed out. The provider may be overloaded — try again.";

        // Agent hit max tool rounds
        if (raw.Contains("Maximum tool rounds", StringComparison.OrdinalIgnoreCase))
            return "The agent reached its tool use limit for this turn. Try breaking your request into smaller steps.";

        return $"{prefix}Something went wrong. {raw}";
    }

    /// <summary>
    /// Extracts the "[Provider · model]" prefix from enriched error messages.
    /// Returns e.g. "OpenRouter · gemma-4:free" or null if not present.
    /// </summary>
    private static string? ExtractProviderContext(string raw)
    {
        if (raw.Length > 2 && raw[0] == '[')
        {
            var end = raw.IndexOf(']', 1);
            if (end > 1) return raw[1..end];
        }
        return null;
    }
}

public sealed class ToolUseModel
{
    public string ToolName { get; set; } = string.Empty;
    public string ToolUseId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public bool IsPendingConfirmation { get; set; }
    public ConfirmationRequest? PendingRequest { get; set; }
}

public sealed class AutoArtifactModel
{
    public string Path { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056", Justification = "Razor href binding requires string")]
    public string? AccessUrl { get; init; }
}
