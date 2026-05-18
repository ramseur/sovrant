namespace Sovrant.Runtime.Conversation;

/// <summary>A discriminated union of events emitted by the conversation runtime during a turn.</summary>
public abstract partial record RuntimeEvent
{
    private RuntimeEvent() { }

    /// <summary>Emitted after routing resolves the provider, before any text streams.</summary>
    public sealed record ModelSelected(string Model, string ProviderName) : RuntimeEvent;

    /// <summary>A streaming text chunk from the model.</summary>
    public sealed record TextChunk(string Text) : RuntimeEvent;

    /// <summary>The model has requested a tool invocation.</summary>
    public sealed record ToolUseRequested(
        string ToolUseId,
        string ToolName,
        System.Text.Json.JsonElement Input) : RuntimeEvent;

    /// <summary>A tool has been executed and its result is available.</summary>
    public sealed record ToolResult(
        string ToolUseId,
        string ToolName,
        string Content,
        bool IsError) : RuntimeEvent;

    /// <summary>The model turn is complete.</summary>
    public sealed record TurnComplete(
        string? StopReason,
        int InputTokens,
        int OutputTokens,
        string? Model = null,
        string? ProviderName = null) : RuntimeEvent;

    /// <summary>Cost estimate for a completed turn (emitted after <see cref="TurnComplete"/> when cost tracking is enabled).</summary>
    public sealed record TurnCost(
        decimal? EstimatedUsd,
        string Source) : RuntimeEvent;

    /// <summary>A tool was denied by the permission policy.</summary>
    public sealed record PermissionDenied(
        string ToolName,
        string Reason) : RuntimeEvent;

    /// <summary>An error occurred during the turn.</summary>
    public sealed record RuntimeError(string Message) : RuntimeEvent;

    /// <summary>
    /// The runtime auto-saved a large text block that the model wrote directly
    /// into chat (instead of calling the Artifact tool). Emitted after the text
    /// stream so the UI can render a download card alongside the inline text.
    /// </summary>
    public sealed record ArtifactWritten(
        string Path,
        string Format,
        long SizeBytes,
        Uri? AccessUrl,
        string RunId,
        string WorkspaceId,
        string ProjectId) : RuntimeEvent;

    /// <summary>
    /// An MCP server requires OAuth authorization before it can be used.
    /// The authorization URL should be surfaced to the user so they can complete the flow.
    /// </summary>
    public sealed record AuthRequired(string ServerName, Uri AuthorizationUrl) : RuntimeEvent;

    // ── Phase 59 events ─────────────────────────────────────────────────

    /// <summary>
    /// Phase 59d — emitted immediately after the intent gate classifies the
    /// user's message. Tells the UI what the system thinks the user wants so
    /// the user sees "I'll create a PDF report" before any tool runs.
    /// </summary>
    public sealed record IntentNarrated(string Narration) : RuntimeEvent;

    /// <summary>
    /// Phase 59a — the intent gate determined the user's message is ambiguous
    /// and needs clarification before tools can be exposed.
    /// </summary>
    public sealed record ClarificationNeeded(string Question) : RuntimeEvent;

    /// <summary>
    /// Phase 59b — a plan has been produced and is being presented to the user
    /// for review and optional approval before execution begins.
    /// </summary>
    public sealed record PlanPresented(
        string PlanId,
        string FormattedPlan,
        bool RequiresApproval) : RuntimeEvent;

    /// <summary>Phase 59b — the user approved the presented plan.</summary>
    public sealed record PlanApproved(string PlanId) : RuntimeEvent;

    /// <summary>Phase 59b — the user rejected the presented plan.</summary>
    public sealed record PlanRejected(string PlanId, string? Reason) : RuntimeEvent;

    /// <summary>
    /// Phase 59e — progress update for a step within a plan execution,
    /// streamed to UIs so the user can track what the system is doing.
    /// </summary>
    public sealed record StepProgress(
        int Current,
        int Total,
        string Intent,
        string Status) : RuntimeEvent;
}
