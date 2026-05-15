using System.Text.Json;
using Sovrant.Runtime.Documents.Templates;

namespace Sovrant.Runtime.Documents.Trust;

/// <summary>
/// Phase 66 acceptance gate that runs before <see cref="IDocumentGenerator"/>
/// is called. Lets policy code (HIPAA / PHI handling, jurisdiction-specific
/// legal review, etc.) refuse generation without templates having to know
/// about it.
/// </summary>
public interface IDocumentTrustGate
{
    DocumentTrustDecision Evaluate(IDocumentTemplate template, JsonElement data);
}

public sealed record DocumentTrustDecision(bool IsAllowed, string? DenyReason)
{
    public static DocumentTrustDecision Allow() => new(true, null);
    public static DocumentTrustDecision Deny(string reason) => new(false, reason);
}
