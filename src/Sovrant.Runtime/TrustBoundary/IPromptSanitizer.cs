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
}
