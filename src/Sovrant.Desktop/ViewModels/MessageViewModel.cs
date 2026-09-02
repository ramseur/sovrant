using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Desktop.Adapters;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Desktop.ViewModels;

public partial class MessageViewModel : ViewModelBase
{
    private static readonly string[] ThinkingPhrases =
    [
        "Thinking...",
        "Reasoning through it...",
        "Gathering thoughts...",
        "Connecting the dots...",
        "Weighing the options...",
        "Working on it...",
    ];

    private DispatcherTimer? _elapsedTimer;
    private readonly Stopwatch _stopwatch = new();

    [ObservableProperty]
    private string _role = "user";

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isUser = true;

    [ObservableProperty]
    private bool _isThinking;

    [ObservableProperty]
    private string _thinkingText = "Thinking really hard...";

    /// <summary>True while text is still streaming in. Raw text is shown instead of markdown.</summary>
    [ObservableProperty]
    private bool _isStreaming;

    /// <summary>True when streaming is done and markdown should be rendered.</summary>
    [ObservableProperty]
    private bool _isComplete;

    /// <summary>Elapsed time string shown while thinking/streaming (e.g. "3s", "1m 12s").</summary>
    [ObservableProperty]
    private string _elapsedText = string.Empty;

    /// <summary>True when the response ended in an error (shows error UI with retry).</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>User-friendly error message.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // ── Phase 59 properties ─────────────────────────────────────────────

    /// <summary>Phase 59d — what the system thinks the user wants, e.g. "I'll create a PDF report for you".</summary>
    [ObservableProperty]
    private string? _intentNarration;

    /// <summary>Phase 59d — summary of what was actually done, derived from tool uses after completion.</summary>
    [ObservableProperty]
    private string? _actionSummary;

    /// <summary>Phase 59a — clarification question from the intent gate.</summary>
    [ObservableProperty]
    private string? _clarificationQuestion;

    /// <summary>Phase 59b — formatted plan text awaiting approval.</summary>
    [ObservableProperty]
    private string? _planContent;

    /// <summary>Phase 59b — whether the presented plan requires user approval.</summary>
    [ObservableProperty]
    private bool _planRequiresApproval;

    /// <summary>Phase 59b — ID of the presented plan.</summary>
    [ObservableProperty]
    private string? _planId;

    /// <summary>Phase 59e — current step index (1-based).</summary>
    [ObservableProperty]
    private int _currentStep;

    /// <summary>Phase 59e — total number of steps in the plan.</summary>
    [ObservableProperty]
    private int _totalSteps;

    /// <summary>Phase 59e — human-readable step progress summary.</summary>
    [ObservableProperty]
    private string? _stepProgressText;

    /// <summary>Phase 59e — whether tools are actively executing (shows status bar).</summary>
    [ObservableProperty]
    private bool _isExecutingTools;

    /// <summary>Status text shown during tool execution (e.g. "Running Read...").</summary>
    [ObservableProperty]
    private string _executionStatusText = string.Empty;

    /// <summary>The model that generated this response (e.g. "deepseek/deepseek-chat:free").</summary>
    [ObservableProperty]
    private string? _modelName;

    /// <summary>The provider that served this response (e.g. "OpenRouter").</summary>
    [ObservableProperty]
    private string? _providerName;

    /// <summary>Display name of the user who sent this message (local part of email). Never a raw ID.</summary>
    [ObservableProperty]
    private string? _userDisplayName;

    /// <summary>Single uppercase letter for the user avatar bubble.</summary>
    public string UserInitial => string.IsNullOrEmpty(UserDisplayName)
        ? "U"
        : UserDisplayName[..1].ToUpperInvariant();

    /// <summary>
    /// Display label for the message sender. Shows "Provider / model" for assistant
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

    /// <summary>Formats a model ID for display (e.g. "deepseek/deepseek-chat:free" → "deepseek-chat:free").</summary>
    private static string FormatModelName(string model)
    {
        // Strip the provider prefix (e.g. "deepseek/" from "deepseek/deepseek-chat:free").
        var slash = model.LastIndexOf('/');
        return slash >= 0 ? model[(slash + 1)..] : model;
    }

    /// <summary>Count of completed tool calls in this message.</summary>
    private int _completedToolCount;

    /// <summary>Raw text passed to the Markdig-based markdown presenter.</summary>
    public string SafeMarkdown => Text;

    public ObservableCollection<ToolUseViewModel> ToolUses { get; } = [];

    // ── Phase 126 — collapsed work strip ────────────────────────────────

    /// <summary>Whether the work strip's tool-list level is expanded.</summary>
    [ObservableProperty]
    private bool _isWorkStripExpanded;

    /// <summary>ToolUseId of the tool currently showing full detail (only one at a time).</summary>
    [ObservableProperty]
    private string? _activeDetailToolId;

    public int NonPendingToolCount => ToolUses.Count(t => !t.IsPendingConfirmation);
    public int ErrorToolCount => ToolUses.Count(t => t.IsError);
    public bool HasWorkStripErrors => ErrorToolCount > 0;
    public bool HasWorkStrip => NonPendingToolCount > 0;
    public string WorkStripCaret => IsWorkStripExpanded ? "▾" : "▸";
    public string WorkStripActionLabel => NonPendingToolCount == 1 ? "1 action" : $"{NonPendingToolCount} actions";
    public string WorkStripErrorLabel => ErrorToolCount == 1 ? "1 error" : $"{ErrorToolCount} errors";

    /// <summary>True when this turn has any tool call at all (pending or otherwise) — used to
    /// decide whether the answer/work-strip separator (Phase 126 #4) should render.</summary>
    public bool HasAnyToolUses => ToolUses.Count > 0;

    /// <summary>Phase 126 #4 — answer-first: shows a thin divider between the completed answer
    /// and the (now-subordinate) work strip / pending tools below it.</summary>
    public bool ShowWorkSeparator => IsComplete && HasAnyToolUses;

    public MessageViewModel()
    {
        ToolUses.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (ToolUseViewModel t in e.NewItems)
                    t.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName is nameof(ToolUseViewModel.IsError)
                            or nameof(ToolUseViewModel.IsPendingConfirmation)
                            or nameof(ToolUseViewModel.Status))
                            RaiseWorkStripChanged();
                    };
            RaiseWorkStripChanged();
        };
    }

    private void RaiseWorkStripChanged()
    {
        OnPropertyChanged(nameof(NonPendingToolCount));
        OnPropertyChanged(nameof(HasAnyToolUses));
        OnPropertyChanged(nameof(ShowWorkSeparator));
        OnPropertyChanged(nameof(ErrorToolCount));
        OnPropertyChanged(nameof(HasWorkStripErrors));
        OnPropertyChanged(nameof(HasWorkStrip));
        OnPropertyChanged(nameof(WorkStripActionLabel));
        OnPropertyChanged(nameof(WorkStripErrorLabel));
    }

    partial void OnIsWorkStripExpandedChanged(bool value) => OnPropertyChanged(nameof(WorkStripCaret));

    [RelayCommand]
    private void ToggleWorkStrip() => IsWorkStripExpanded = !IsWorkStripExpanded;

    [RelayCommand]
    private void ToggleToolDetail(ToolUseViewModel tool)
    {
        var activate = ActiveDetailToolId != tool.ToolUseId;
        ActiveDetailToolId = activate ? tool.ToolUseId : null;
        foreach (var t in ToolUses)
            t.IsActiveDetail = activate && t.ToolUseId == tool.ToolUseId;
    }

    /// <summary>Artifacts auto-saved from large text blocks (not tied to a specific tool use row).</summary>
    public ObservableCollection<DocumentArtifactViewModel> StandaloneArtifacts { get; } = [];
    public bool HasStandaloneArtifacts => StandaloneArtifacts.Count > 0;

    // ── Phase 116 H — provenance ────────────────────────────────────────

    /// <summary>Turn index this message corresponds to (0-based). Used to load attributions.</summary>
    [ObservableProperty]
    private int _turnIndex;

    /// <summary>Knowledge items invoked during this turn — populated after CompleteStreaming.</summary>
    public ObservableCollection<KnowledgeAttribution> Sources { get; } = [];

    [ObservableProperty]
    private bool _hasSources;

    public void AddStandaloneArtifact(DocumentArtifactViewModel vm)
    {
        StandaloneArtifacts.Add(vm);
        OnPropertyChanged(nameof(HasStandaloneArtifacts));
    }

    partial void OnRoleChanged(string value) => IsUser = value == "user";
    partial void OnModelNameChanged(string? value) => OnPropertyChanged(nameof(SenderLabel));
    partial void OnProviderNameChanged(string? value) => OnPropertyChanged(nameof(SenderLabel));
    partial void OnIsCompleteChanged(bool value) => OnPropertyChanged(nameof(ShowWorkSeparator));

    public void StartThinking(string? prompt = null)
    {
        IsThinking = true;
        ThinkingText = PickThinkingPhrase(prompt);

        // Start elapsed timer — ticks every second.
        _stopwatch.Restart();
        ElapsedText = "0s";
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) => ElapsedText = FormatElapsed(_stopwatch.Elapsed);
        _elapsedTimer.Start();
    }

    public void StopThinking()
    {
        IsThinking = false;
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
            if (ContainsAny(p, "create", "make", "generate", "build", "write"))
                return "Working on your request...";
        }
        return ThinkingPhrases[RandomNumberGenerator.GetInt32(ThinkingPhrases.Length)];
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));

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

    public void StartStreaming()
    {
        IsStreaming = true;
        IsComplete = false;
    }

    public void CompleteStreaming()
    {
        IsStreaming = false;
        IsComplete = true;
        IsExecutingTools = false;
        StopElapsedTimer();
        OnPropertyChanged(nameof(SafeMarkdown));
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

    private void StopElapsedTimer()
    {
        _stopwatch.Stop();
        _elapsedTimer?.Stop();
        _elapsedTimer = null;
        ElapsedText = FormatElapsed(_stopwatch.Elapsed);
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        return $"{(int)elapsed.TotalSeconds}s";
    }

    public void SetError(string rawError)
    {
        StopThinking();
        StopElapsedTimer();
        IsStreaming = false;
        HasError = true;
        ErrorMessage = FriendlyError(rawError);
        // If there was partial text, still mark complete so it renders.
        if (!string.IsNullOrEmpty(Text))
        {
            IsComplete = true;
            OnPropertyChanged(nameof(SafeMarkdown));
        }
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

    public void AppendText(string chunk)
    {
        if (IsThinking) StopThinking();
        if (!IsStreaming) StartStreaming();
        Text += chunk;
    }

    public void AddToolUse(string toolName, string toolUseId)
    {
        // Stop the thinking carousel — the execution status bar takes over.
        if (IsThinking) StopThinking();

        IsExecutingTools = true;
        ExecutionStatusText = FriendlyToolStatus(toolName);

        ToolUses.Add(new ToolUseViewModel
        {
            ToolName = FriendlyToolLabel(toolName),
            ToolUseId = toolUseId,
            Status = "Running...",
        });
    }

    public void AddConfirmation(ConfirmationRequest request)
    {
        ToolUses.Add(new ToolUseViewModel
        {
            ToolName = request.ToolName,
            ToolUseId = string.Empty,
            Status = "Awaiting approval...",
            IsPendingConfirmation = true,
            PendingRequest = request,
            Result = request.Input.ToString(),
        });
    }

    public void UpdateToolResult(string toolUseId, string content, bool isError)
    {
        foreach (var tu in ToolUses)
        {
            if (tu.ToolUseId == toolUseId)
            {
                tu.Result = content;
                tu.IsError = isError;
                tu.Status = isError ? "Error" : "Done";
                _completedToolCount++;
                ExecutionStatusText = $"Completed {_completedToolCount}/{ToolUses.Count} tool calls";
                break;
            }
        }
    }

    /// <summary>
    /// Escapes HTML angle brackets outside of fenced code blocks so the
    /// markdown renderer doesn't try to interpret them as real HTML.
    /// </summary>
    private static string EscapeHtmlOutsideCodeBlocks(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var sb = new System.Text.StringBuilder(text.Length);
        bool inCodeBlock = false;
        foreach (var line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                inCodeBlock = !inCodeBlock;

            if (inCodeBlock)
            {
                sb.AppendLine(line);
            }
            else
            {
                // Escape < and > outside code blocks, but preserve markdown-safe uses.
                sb.AppendLine(line.Replace("<", "&lt;", StringComparison.Ordinal)
                                  .Replace(">", "&gt;", StringComparison.Ordinal));
            }
        }

        // Remove trailing newline added by AppendLine.
        if (sb.Length >= Environment.NewLine.Length)
            sb.Length -= Environment.NewLine.Length;

        return sb.ToString();
    }
}

public partial class ToolUseViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private string _toolUseId = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isPendingConfirmation;

    /// <summary>Phase 126 — true when this row's full detail is shown in the work strip (only one at a time).</summary>
    [ObservableProperty]
    private bool _isActiveDetail;

    public ConfirmationRequest? PendingRequest { get; set; }

    public bool HasResult => !string.IsNullOrEmpty(Result);

    public bool IsStatusDone => !IsError && (Status == "Done" || Status == "Approved");
    public bool IsStatusError => IsError || Status == "Denied";
    public bool IsStatusRunning => !IsStatusDone && !IsStatusError;

    /// <summary>
    /// Phase 66 — populated from the JSON result of document-generation tools
    /// (<c>DocumentGenerate</c>, <c>DocumentFromTemplate</c>, <c>DocumentPackage</c>).
    /// Empty for every other tool.
    /// </summary>
    public ObservableCollection<DocumentArtifactViewModel> DocumentArtifacts { get; } = new();

    public bool HasDocumentArtifacts => DocumentArtifacts.Count > 0;

    partial void OnResultChanged(string value)
    {
        RebuildDocumentArtifacts();
        OnPropertyChanged(nameof(HasResult));
    }

    partial void OnToolNameChanged(string value) => RebuildDocumentArtifacts();

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(IsStatusDone));
        OnPropertyChanged(nameof(IsStatusError));
        OnPropertyChanged(nameof(IsStatusRunning));
    }

    partial void OnIsErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStatusDone));
        OnPropertyChanged(nameof(IsStatusError));
        OnPropertyChanged(nameof(IsStatusRunning));
    }

    private void RebuildDocumentArtifacts()
    {
        DocumentArtifacts.Clear();
        if (!DocumentArtifactParser.ProducesArtifacts(ToolName) || string.IsNullOrWhiteSpace(Result))
        {
            OnPropertyChanged(nameof(HasDocumentArtifacts));
            return;
        }
        foreach (var art in DocumentArtifactParser.Parse(ToolName, Result))
            DocumentArtifacts.Add(art);
        OnPropertyChanged(nameof(HasDocumentArtifacts));
    }

    [RelayCommand]
    private void Approve()
    {
        PendingRequest?.Approve();
        IsPendingConfirmation = false;
        Status = "Approved";
    }

    [RelayCommand]
    private void ApproveForTurn()
    {
        PendingRequest?.ApproveForTurn();
        IsPendingConfirmation = false;
        Status = "Approved (turn)";
    }

    [RelayCommand]
    private void Deny()
    {
        PendingRequest?.Deny();
        IsPendingConfirmation = false;
        Status = "Denied";
        IsError = true;
    }
}
