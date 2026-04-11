using System.Security.Cryptography;
using Sovrant.Web.Adapters;

namespace Sovrant.Web.Components.Shared;

public sealed class ChatMessageModel
{
    private static readonly string[] ThinkingPhrases =
    [
        "Thinking really hard...", "Consulting the oracle...", "Pondering the possibilities...",
        "Gathering thoughts...", "Connecting the dots...", "Reasoning through it...",
        "Weighing the options...", "Mulling it over...",
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
    public List<ToolUseModel> ToolUses { get; } = [];

    public void StartThinking()
    {
        IsThinking = true;
        var idx = RandomNumberGenerator.GetInt32(ThinkingPhrases.Length);
        ThinkingText = ThinkingPhrases[idx];
    }

    public void StopThinking() => IsThinking = false;

    public void AppendText(string chunk)
    {
        if (IsThinking) StopThinking();
        if (!IsStreaming) { IsStreaming = true; IsComplete = false; }
        Text += chunk;
    }

    public void CompleteStreaming()
    {
        IsStreaming = false;
        IsComplete = true;
    }

    public void SetError(string rawError)
    {
        StopThinking();
        IsStreaming = false;
        HasError = true;
        ErrorMessage = FriendlyError(rawError);
        if (!string.IsNullOrEmpty(Text)) IsComplete = true;
    }

    private static string FriendlyError(string raw)
    {
        if (raw.Contains("Provider returned error", StringComparison.OrdinalIgnoreCase))
            return "The model provider returned an error. Try again or switch to a different model.";
        if (raw.Contains("400", StringComparison.Ordinal) && raw.Contains("API error", StringComparison.OrdinalIgnoreCase))
            return "The model rejected this request (400). Try starting a new chat.";
        if (raw.Contains("429", StringComparison.Ordinal) || raw.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            return "Rate limited. Wait a moment and try again.";
        if (raw.Contains("401", StringComparison.Ordinal) || raw.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
            return "Authentication failed. Check your API key in Settings.";
        if (raw.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "The request timed out. Try again.";
        return $"Something went wrong: {raw}";
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
