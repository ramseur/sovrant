using System.Collections.ObjectModel;
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

    /// <summary>True when the response ended in an error (shows error UI with retry).</summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>User-friendly error message.</summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>Raw text passed to the Markdig-based markdown presenter.</summary>
    public string SafeMarkdown => Text;

    public ObservableCollection<ToolUseViewModel> ToolUses { get; } = [];

    partial void OnRoleChanged(string value) => IsUser = value == "user";

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
        OnPropertyChanged(nameof(SafeMarkdown));
    }

    public void SetError(string rawError)
    {
        StopThinking();
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
        if (raw.Contains("Provider returned error", StringComparison.OrdinalIgnoreCase))
            return "The model provider returned an error. This can happen with free-tier models under load. Try again or switch to a different model.";
        if (raw.Contains("400", StringComparison.Ordinal) && raw.Contains("API error", StringComparison.OrdinalIgnoreCase))
            return "The model rejected this request (400). The conversation may be too long or the model doesn't support this format. Try starting a new chat.";
        if (raw.Contains("429", StringComparison.Ordinal) || raw.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "Rate limited — too many requests. Wait a moment and try again.";
        if (raw.Contains("401", StringComparison.Ordinal) || raw.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
            return "Authentication failed. Check your API key in Settings.";
        if (raw.Contains("connection", StringComparison.OrdinalIgnoreCase) && raw.Contains("refused", StringComparison.OrdinalIgnoreCase))
            return "Could not connect to the model provider. Check your internet connection and base URL.";
        if (raw.Contains("No such host", StringComparison.OrdinalIgnoreCase))
            return "Could not reach the provider — DNS lookup failed. Check your internet connection.";
        if (raw.Contains("timeout", StringComparison.OrdinalIgnoreCase) || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase))
            return "The request timed out. The provider may be overloaded — try again.";
        return $"Something went wrong: {raw}";
    }

    public void AppendText(string chunk)
    {
        if (IsThinking) StopThinking();
        if (!IsStreaming) StartStreaming();
        Text += chunk;
    }

    public void AddToolUse(string toolName, string toolUseId)
    {
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

    public ConfirmationRequest? PendingRequest { get; set; }

    [RelayCommand]
    private void Approve()
    {
        PendingRequest?.Approve();
        IsPendingConfirmation = false;
        Status = "Approved";
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
