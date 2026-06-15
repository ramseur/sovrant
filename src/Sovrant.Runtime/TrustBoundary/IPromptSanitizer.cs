using Sovrant.Api.Types;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Result of sanitizing a messages request.</summary>
/// <param name="Request">The sanitized request with placeholders replacing sensitive data.</param>
/// <param name="Map">The redaction map for restoring originals on the response path.</param>
public sealed record SanitizationResult(MessagesRequest Request, RedactionMap Map);

/// <summary>Sanitizes outbound LLM requests and restores inbound responses.</summary>
public interface IPromptSanitizer
{
    /// <summary>Scans and sanitizes all text fields in a messages request.</summary>
    SanitizationResult Sanitize(MessagesRequest request);

    /// <summary>Restores placeholders in a response string using the redaction map.</summary>
    string Restore(string response, RedactionMap map);

    /// <summary>
    /// Sanitizes a raw string one-way — no <see cref="RedactionMap"/> is kept and
    /// redactions cannot be restored. Use for knowledge content injected into the
    /// system prompt (Step B) and for tool results stored in history and
    /// <c>session_entries</c> (Step C), where PII must never appear in storage or
    /// reach the LLM. Returns the input unchanged when sanitization is disabled.
    /// </summary>
    string SanitizeRawText(string text);
}
