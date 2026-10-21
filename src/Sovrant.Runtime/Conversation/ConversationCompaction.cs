using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sovrant.Api.Types;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Phase 148 — the rules for summarising older messages so a long conversation fits the model:
/// when to start, where to cut, what to keep word for word, and how the summary is saved so a
/// reopened conversation sends the same summary instead of the full history again.
/// </summary>
public static class ConversationCompaction
{
    /// <summary>
    /// Note chats show where older messages were summarised. The full messages stay in the saved
    /// conversation and on screen.
    /// </summary>
    public const string ChatNote = "Earlier messages were summarised to fit the model's context window.";

    /// <summary>Text for a saved compaction entry (exports): the summary, or the note saved before Phase 148.</summary>
    public static string DisplayText(string content) => Parse(content)?.Summary ?? content;

    /// <summary>Compaction starts when a request reaches this share of the model's context window.</summary>
    internal const double CompactAtShareOfWindow = 0.75;

    /// <summary>The newest messages kept word for word take up to this share of the threshold.</summary>
    internal const double KeepShareOfThreshold = 0.25;

    /// <summary>Longest text kept word for word for each pinned item.</summary>
    internal const int MaxPinnedChars = 4000;

    /// <summary>The reply that follows the summary when the kept messages start with a request.</summary>
    internal const string Acknowledgement = "Understood. I have the context from the conversation summary.";

    /// <summary>Session entry role that records a compaction.</summary>
    public const string EntryRole = "compaction";

    private static readonly JsonSerializerOptions s_json = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// The input-token count that triggers compaction: 75% of the model's context window when it is
    /// known. <c>SOVRANT_COMPACT_THRESHOLD</c> wins when the env override (<c>SOVRANT_ENV_OVERRIDE</c>)
    /// is on; otherwise the configured threshold is used only when the window is unknown. 0
    /// (configured) turns compaction off.
    /// </summary>
    internal static int Threshold(int configured, int? contextWindow, bool envOverride)
    {
        if (configured <= 0) return 0;
        if (envOverride) return configured;
        return contextWindow is > 0 ? (int)(contextWindow.Value * CompactAtShareOfWindow) : configured;
    }

    /// <summary>Rough token estimate (about four characters per token).</summary>
    internal static int EstimateTokens(InputMessage message)
    {
        var chars = 0;
        foreach (var block in message.Content)
        {
            chars += block switch
            {
                InputContentBlock.TextBlock t => t.Text.Length,
                InputContentBlock.ToolUseBlock u => u.Name.Length + u.Input.GetRawText().Length,
                InputContentBlock.ToolResultBlock r => ResultText(r).Length,
                _ => 0,
            };
        }
        return (chars / 4) + 4;
    }

    /// <summary>Rough token estimate for a whole request.</summary>
    internal static int EstimateTokens(IReadOnlyList<InputMessage> history, string? systemPrompt)
    {
        var total = (systemPrompt?.Length ?? 0) / 4;
        foreach (var m in history) total += EstimateTokens(m);
        return total;
    }

    /// <summary>
    /// Index of the first message kept word for word; everything before it is summarised. Keeps the
    /// newest messages up to <see cref="KeepShareOfThreshold"/> of the threshold (always at least the
    /// latest one) and never starts on a tool result whose call would be summarised away. Returns a
    /// value below 2 when there is not enough to summarise.
    /// </summary>
    internal static int ChooseCut(IReadOnlyList<InputMessage> history, int threshold)
    {
        if (history.Count < 3) return 0;
        var budget = Math.Max(1, (int)(threshold * KeepShareOfThreshold));
        var cut = history.Count - 1;
        var kept = EstimateTokens(history[cut]);
        while (cut > 0)
        {
            var next = EstimateTokens(history[cut - 1]);
            if (kept + next > budget) break;
            kept += next;
            cut--;
        }
        while (cut > 0 && IsToolResults(history[cut])) cut--;
        return cut;
    }

    /// <summary>The summarised messages as plain text for the summariser, tool calls included in brief.</summary>
    internal static string Describe(IEnumerable<InputMessage> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(m.Role.ToUpperInvariant()).Append(':');
            foreach (var block in m.Content)
            {
                switch (block)
                {
                    case InputContentBlock.TextBlock t:
                        sb.Append(' ').Append(t.Text);
                        break;
                    case InputContentBlock.ToolUseBlock u:
                        sb.Append(" [called ").Append(u.Name).Append(' ').Append(Clip(u.Input.GetRawText(), 300)).Append(']');
                        break;
                    case InputContentBlock.ToolResultBlock r:
                        sb.Append(r.IsError ? " [tool error: " : " [tool result: ").Append(Clip(ResultText(r), 1000)).Append(']');
                        break;
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Items kept word for word across compactions: the first request, the request being worked
    /// on, the plan the person approved (the assistant text before ExitPlanMode) and the latest
    /// tool error. Values found in <paramref name="summarised"/> replace older ones. The request
    /// being worked on is pinned only when no request is among the <paramref name="kept"/> messages.
    /// </summary>
    internal static CompactionPins Pin(IReadOnlyList<InputMessage> summarised, IReadOnlyList<InputMessage> kept, CompactionPins previous)
    {
        string? first = previous.OriginalRequest, current = previous.CurrentRequest, plan = previous.ApprovedPlan;
        string? error = previous.LatestToolError, errorTool = previous.LatestToolErrorTool;
        var toolNames = new Dictionary<string, string>(StringComparer.Ordinal);
        string? lastAssistantText = null;

        foreach (var m in summarised)
        {
            if (m.Role == "user" && !IsToolResults(m))
            {
                var text = Text(m);
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith(SummaryHeading, StringComparison.Ordinal)) continue;
                first ??= Clip(text, MaxPinnedChars);
                current = Clip(text, MaxPinnedChars);
            }
            else if (m.Role == "assistant")
            {
                var text = Text(m);
                if (!string.IsNullOrWhiteSpace(text) && text != Acknowledgement) lastAssistantText = text;
                foreach (var u in m.Content.OfType<InputContentBlock.ToolUseBlock>())
                {
                    toolNames[u.Id] = u.Name;
                    if (u.Name == "ExitPlanMode" && lastAssistantText is not null)
                        plan = Clip(lastAssistantText, MaxPinnedChars);
                }
            }
            else
            {
                foreach (var r in m.Content.OfType<InputContentBlock.ToolResultBlock>().Where(r => r.IsError))
                {
                    error = Clip(ResultText(r), MaxPinnedChars);
                    errorTool = toolNames.GetValueOrDefault(r.ToolUseId);
                }
            }
        }

        if (kept.Any(m => m.Role == "user" && !IsToolResults(m) && !string.IsNullOrWhiteSpace(Text(m)))) current = null;
        return new CompactionPins(first, current == first ? null : current, plan, error, errorTool);
    }

    /// <summary>First line of every summary message; marks it so it is never pinned as a request.</summary>
    internal const string SummaryHeading = "[Earlier conversation, summarised to fit the model's context window]";

    /// <summary>The message that stands in for the summarised messages.</summary>
    internal static string SummaryMessage(string summary, CompactionPins pins)
    {
        var sb = new StringBuilder(SummaryHeading).Append("\n\n").Append(summary.Trim());
        if (pins.IsEmpty) return sb.ToString();
        sb.Append("\n\nKept word for word:");
        if (pins.OriginalRequest is { } o) sb.Append("\n\nOriginal request:\n").Append(o);
        if (pins.CurrentRequest is { } c) sb.Append("\n\nRequest being worked on:\n").Append(c);
        if (pins.ApprovedPlan is { } p) sb.Append("\n\nApproved plan:\n").Append(p);
        if (pins.LatestToolError is { } e)
            sb.Append("\n\nLatest tool error").Append(pins.LatestToolErrorTool is { } t ? $" ({t})" : string.Empty).Append(":\n").Append(e);
        return sb.ToString();
    }

    /// <summary>
    /// The messages that replace the history: the summary, an acknowledgement when the kept
    /// messages start with a request (or there are none), then the kept messages.
    /// </summary>
    internal static List<InputMessage> Rebuild(string summaryMessage, IReadOnlyList<InputMessage> kept)
    {
        var result = new List<InputMessage>(kept.Count + 2) { InputMessage.UserText(summaryMessage) };
        if (kept.Count == 0 || kept[0].Role == "user")
            result.Add(InputMessage.AssistantText(Acknowledgement));
        result.AddRange(kept);
        return result;
    }

    /// <summary>
    /// How many of <paramref name="kept"/> are saved as session entries (requests and replies with
    /// text; tool calls and results are not saved), so a reload can find them again.
    /// </summary>
    internal static int SavedEntryCount(IEnumerable<InputMessage> kept) =>
        kept.Count(m => (m.Role == "user" && !IsToolResults(m) && !string.IsNullOrEmpty(Text(m)))
                        || (m.Role == "assistant" && !string.IsNullOrEmpty(Text(m))));

    /// <summary>Content of the saved compaction entry.</summary>
    internal static string Serialize(CompactionRecord record) => JsonSerializer.Serialize(record, s_json);

    /// <summary>Reads a saved compaction entry; null for entries saved before Phase 148 (plain text).</summary>
    internal static CompactionRecord? Parse(string content)
    {
        if (string.IsNullOrEmpty(content) || content[0] != '{') return null;
        try
        {
            var record = JsonSerializer.Deserialize<CompactionRecord>(content, s_json);
            return string.IsNullOrEmpty(record?.Summary) ? null : record;
        }
        catch (JsonException) { return null; }
    }

    private static bool IsToolResults(InputMessage m) =>
        m.Role == "user" && m.Content.Any(b => b is InputContentBlock.ToolResultBlock);

    private static string Text(InputMessage m) =>
        string.Join(" ", m.Content.OfType<InputContentBlock.TextBlock>().Select(b => b.Text));

    private static string ResultText(InputContentBlock.ToolResultBlock r) =>
        string.Join(" ", r.Content.OfType<ToolResultContentBlock.TextBlock>().Select(b => b.Text));

    private static string Clip(string text, int max) =>
        text.Length <= max ? text : string.Concat(text.AsSpan(0, max), "…");
}

/// <summary>Phase 148 — items kept word for word when older messages are summarised.</summary>
internal sealed record CompactionPins(
    [property: JsonPropertyName("original_request")] string? OriginalRequest = null,
    [property: JsonPropertyName("current_request")] string? CurrentRequest = null,
    [property: JsonPropertyName("approved_plan")] string? ApprovedPlan = null,
    [property: JsonPropertyName("latest_tool_error")] string? LatestToolError = null,
    [property: JsonPropertyName("latest_tool_error_tool")] string? LatestToolErrorTool = null)
{
    internal static readonly CompactionPins None = new();

    [JsonIgnore]
    internal bool IsEmpty => OriginalRequest is null && CurrentRequest is null && ApprovedPlan is null && LatestToolError is null;
}

/// <summary>
/// Phase 148 — what a compaction session entry stores: the summary message, how many of the
/// saved entries before it were kept word for word, and the pinned items.
/// </summary>
internal sealed record CompactionRecord(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("kept")] int Kept,
    [property: JsonPropertyName("summarised")] int Summarised,
    [property: JsonPropertyName("tokens")] int Tokens,
    [property: JsonPropertyName("pins")] CompactionPins? Pins);
