using Sovrant.Api.Routing;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59a — pre-LLM intent gate that replaces the crude
/// <c>LooksLikeToolRequest()</c> substring matcher. Classifies the
/// user's message semantically and decides whether tools should be
/// exposed, the user should be asked to clarify, or the message is
/// purely conversational.
/// </summary>
public interface IIntentGate
{
    /// <summary>
    /// Classifies the user message and returns a gate result that the
    /// conversation runtime uses to decide tool exposure and clarification.
    /// </summary>
    Task<IntentGateResult> ClassifyAsync(
        string message,
        int conversationDepth = 0,
        CancellationToken ct = default);
}

/// <summary>The result of intent gate classification.</summary>
/// <param name="Intent">The classified intent.</param>
/// <param name="Confidence">Classification confidence (0.0–1.0).</param>
/// <param name="RequiresTools">Whether tools should be exposed to the LLM for this message.</param>
/// <param name="NeedsClarification">Whether the system should ask the user to clarify before proceeding.</param>
/// <param name="SuggestedClarification">If clarification is needed, the question to ask the user.</param>
/// <param name="Narration">Phase 59d — short human-readable phrase describing what the system thinks the user wants, e.g. "I'll create a PDF report for you". Null for conversational messages.</param>
public sealed record IntentGateResult(
    IntentClass Intent,
    float Confidence,
    bool RequiresTools,
    bool NeedsClarification,
    string? SuggestedClarification,
    string? Narration = null);
