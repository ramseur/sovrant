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
    public bool IsExecutingTools { get; set; }
    public string ExecutionStatusText { get; set; } = string.Empty;
    private int _completedToolCount;
    public List<ToolUseModel> ToolUses { get; } = [];

    /// <summary>The model that generated this response.</summary>
    public string? ModelName { get; set; }

    /// <summary>The provider that served this response.</summary>
    public string? ProviderName { get; set; }

    /// <summary>
    /// Display label for the message sender. Shows "Provider · model" for assistant
    /// messages once the turn completes, falls back to "Sovrant" while streaming.
    /// </summary>
    public string SenderLabel
    {
        get
        {
            if (Role == "user") return "You";
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

    public void AddToolUse(string toolName, string toolUseId)
    {
        if (IsThinking) StopThinking();
        IsExecutingTools = true;
        ExecutionStatusText = $"Running {toolName}...";
        ToolUses.Add(new ToolUseModel { ToolName = toolName, ToolUseId = toolUseId, Status = "Running..." });
    }

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
        IsStreaming = false;
        IsComplete = true;
        IsExecutingTools = false;
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
