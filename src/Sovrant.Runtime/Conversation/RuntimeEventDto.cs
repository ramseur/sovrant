using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Conversation;

/// <summary>
/// Serializable DTO for streaming <see cref="RuntimeEvent"/> subtypes over SignalR
/// or other transports. Uses a string <see cref="Type"/> discriminator to avoid
/// polluting the core runtime type with serialization attributes.
/// </summary>
public sealed class RuntimeEventDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; init; }

    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; init; }

    [JsonPropertyName("tool_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolName { get; init; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; init; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; init; }

    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; init; }

    [JsonPropertyName("stop_reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StopReason { get; init; }

    [JsonPropertyName("input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OutputTokens { get; init; }

    [JsonPropertyName("estimated_usd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? EstimatedUsd { get; init; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonPropertyName("question")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Question { get; init; }

    [JsonPropertyName("plan_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlanId { get; init; }

    [JsonPropertyName("formatted_plan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FormattedPlan { get; init; }

    [JsonPropertyName("requires_approval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RequiresApproval { get; init; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    [JsonPropertyName("current")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Current { get; init; }

    [JsonPropertyName("total")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Total { get; init; }

    [JsonPropertyName("intent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Intent { get; init; }

    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; init; }

    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    [JsonPropertyName("provider_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProviderName { get; init; }

    [JsonPropertyName("server_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerName { get; init; }

    [JsonPropertyName("authorization_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON serialization DTO")]
    public string? AuthorizationUrl { get; init; }

    /// <summary>Maps a core <see cref="RuntimeEvent"/> to its DTO form.</summary>
    public static RuntimeEventDto FromEvent(RuntimeEvent ev) => ev switch
    {
        RuntimeEvent.ModelSelected e => new() { Type = "model_selected", Model = e.Model, ProviderName = e.ProviderName },
        RuntimeEvent.TextChunk e => new() { Type = "text_chunk", Text = e.Text },
        RuntimeEvent.ToolUseRequested e => new() { Type = "tool_use_requested", ToolUseId = e.ToolUseId, ToolName = e.ToolName, Input = e.Input },
        RuntimeEvent.ToolResult e => new() { Type = "tool_result", ToolUseId = e.ToolUseId, ToolName = e.ToolName, Content = e.Content, IsError = e.IsError },
        RuntimeEvent.TurnComplete e => new() { Type = "turn_complete", StopReason = e.StopReason, InputTokens = e.InputTokens, OutputTokens = e.OutputTokens, Model = e.Model, ProviderName = e.ProviderName },
        RuntimeEvent.TurnCost e => new() { Type = "turn_cost", EstimatedUsd = e.EstimatedUsd, Source = e.Source },
        RuntimeEvent.PermissionDenied e => new() { Type = "permission_denied", ToolName = e.ToolName, Reason = e.Reason },
        RuntimeEvent.RuntimeError e => new() { Type = "runtime_error", Message = e.Message },
        RuntimeEvent.AuthRequired e => new() { Type = "auth_required", ServerName = e.ServerName, AuthorizationUrl = e.AuthorizationUrl.ToString() },
        RuntimeEvent.IntentNarrated e => new() { Type = "intent_narrated", Text = e.Narration },
        RuntimeEvent.ClarificationNeeded e => new() { Type = "clarification_needed", Question = e.Question },
        RuntimeEvent.PlanPresented e => new() { Type = "plan_presented", PlanId = e.PlanId, FormattedPlan = e.FormattedPlan, RequiresApproval = e.RequiresApproval },
        RuntimeEvent.PlanApproved e => new() { Type = "plan_approved", PlanId = e.PlanId },
        RuntimeEvent.PlanRejected e => new() { Type = "plan_rejected", PlanId = e.PlanId, Reason = e.Reason },
        RuntimeEvent.StepProgress e => new() { Type = "step_progress", Current = e.Current, Total = e.Total, Intent = e.Intent, Status = e.Status },
        _ => new() { Type = "unknown" },
    };

    /// <summary>Maps a DTO back to a core <see cref="RuntimeEvent"/>.</summary>
    public RuntimeEvent? ToEvent() => Type switch
    {
        "model_selected" => new RuntimeEvent.ModelSelected(Model ?? string.Empty, ProviderName ?? string.Empty),
        "text_chunk" => new RuntimeEvent.TextChunk(Text ?? string.Empty),
        "tool_use_requested" => new RuntimeEvent.ToolUseRequested(ToolUseId ?? string.Empty, ToolName ?? string.Empty, Input ?? default),
        "tool_result" => new RuntimeEvent.ToolResult(ToolUseId ?? string.Empty, ToolName ?? string.Empty, Content ?? string.Empty, IsError ?? false),
        "turn_complete" => new RuntimeEvent.TurnComplete(StopReason, InputTokens ?? 0, OutputTokens ?? 0, Model, ProviderName),
        "turn_cost" => new RuntimeEvent.TurnCost(EstimatedUsd, Source ?? string.Empty),
        "permission_denied" => new RuntimeEvent.PermissionDenied(ToolName ?? string.Empty, Reason ?? string.Empty),
        "runtime_error" => new RuntimeEvent.RuntimeError(Message ?? string.Empty),
        "auth_required" => new RuntimeEvent.AuthRequired(ServerName ?? string.Empty, new Uri(AuthorizationUrl ?? "about:blank")),
        "intent_narrated" => new RuntimeEvent.IntentNarrated(Text ?? string.Empty),
        "clarification_needed" => new RuntimeEvent.ClarificationNeeded(Question ?? string.Empty),
        "plan_presented" => new RuntimeEvent.PlanPresented(PlanId ?? string.Empty, FormattedPlan ?? string.Empty, RequiresApproval ?? false),
        "plan_approved" => new RuntimeEvent.PlanApproved(PlanId ?? string.Empty),
        "plan_rejected" => new RuntimeEvent.PlanRejected(PlanId ?? string.Empty, Reason),
        "step_progress" => new RuntimeEvent.StepProgress(Current ?? 0, Total ?? 0, Intent ?? string.Empty, Status ?? string.Empty),
        _ => null,
    };
}
