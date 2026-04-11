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

    /// <summary>Text with HTML escaped outside code blocks, safe for markdown rendering.</summary>
    public string SafeMarkdown => EscapeHtmlOutsideCodeBlocks(Text);

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
