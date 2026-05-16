using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sovrant.Desktop.Adapters;

namespace Sovrant.Desktop.ViewModels;

public partial class MessageViewModel : ViewModelBase
{
    private static readonly string[] ThinkingPhrases =
    [
        "Thinking really hard...",
        "Consulting the oracle...",
        "Pondering the possibilities...",
        "Gathering thoughts...",
        "Connecting the dots...",
        "Reasoning through it...",
        "Weighing the options...",
        "Mulling it over...",
    ];

    private DispatcherTimer? _thinkingTimer;
    private DispatcherTimer? _elapsedTimer;
    private readonly Stopwatch _stopwatch = new();
    private int _phraseIndex;

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

    partial void OnRoleChanged(string value) => IsUser = value == "user";
    partial void OnModelNameChanged(string? value) => OnPropertyChanged(nameof(SenderLabel));
    partial void OnProviderNameChanged(string? value) => OnPropertyChanged(nameof(SenderLabel));

    public void StartThinking()
    {
        IsThinking = true;
        _phraseIndex = RandomNumberGenerator.GetInt32(ThinkingPhrases.Length);
        ThinkingText = ThinkingPhrases[_phraseIndex];
        _thinkingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _thinkingTimer.Tick += (_, _) =>
        {
            _phraseIndex = (_phraseIndex + 1) % ThinkingPhrases.Length;
            ThinkingText = ThinkingPhrases[_phraseIndex];
        };
        _thinkingTimer.Start();

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
        _thinkingTimer?.Stop();
        _thinkingTimer = null;
    }

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

        if (raw.Contains("429", StringComparison.Ordinal) || raw.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Rate limited by the provider. Wait a moment and try again, or switch to a different model in Settings.";
        if (raw.Contains("401", StringComparison.Ordinal) || raw.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Authentication failed. Check your API key in Settings.";
        if (raw.Contains("403", StringComparison.Ordinal))
            return $"{prefix}Access denied. Your API key may not have permission for this model.";
        if (raw.Contains("400", StringComparison.Ordinal) && raw.Contains("API error", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The model rejected this request (400). Try starting a new chat or switching models.";
        if (raw.Contains("Provider returned error", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The provider returned an error. Try again or switch to a different model in Settings.";
        if (raw.Contains("connection", StringComparison.OrdinalIgnoreCase) && raw.Contains("refused", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Could not connect to the provider. Check your internet connection and base URL.";
        if (raw.Contains("No such host", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}Could not reach the provider — DNS lookup failed. Check your internet connection.";
        if (raw.Contains("timeout", StringComparison.OrdinalIgnoreCase) || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}The request timed out. The provider may be overloaded — try again.";
        if (raw.Contains("No provider available", StringComparison.OrdinalIgnoreCase))
            return "No provider is available. Check your API key and provider settings.";
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
        ExecutionStatusText = $"Running {toolName}...";

        ToolUses.Add(new ToolUseViewModel
        {
            ToolName = toolName,
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

    [ObservableProperty]
    private bool _isExpanded;

    public ConfirmationRequest? PendingRequest { get; set; }

    public bool HasResult => !string.IsNullOrEmpty(Result);

    public bool IsResultLong
    {
        get
        {
            if (string.IsNullOrEmpty(Result)) return false;
            if (Result.Length > 400) return true;
            int nl = 0;
            foreach (var ch in Result) { if (ch == '\n') { nl++; if (nl > 6) return true; } }
            return false;
        }
    }

    public int ResultMaxLines => IsExpanded ? 10000 : 6;

    public string ExpandToggleText => IsExpanded ? "▾ Show less" : "▸ Show more";

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
        OnPropertyChanged(nameof(IsResultLong));
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

    partial void OnIsExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ResultMaxLines));
        OnPropertyChanged(nameof(ExpandToggleText));
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

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
