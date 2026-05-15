using System.Text.Json;
using Sovrant.Runtime.Documents.Templates;

namespace Sovrant.Runtime.Documents.Trust;

/// <summary>
/// Default <see cref="IDocumentTrustGate"/>. For any template whose
/// <see cref="IDocumentTemplate.Industry"/> is <c>healthcare</c>, the
/// caller must pass <c>"consent_acknowledged": true</c> in the data
/// object — an explicit signal that PHI handling is acknowledged.
/// All other templates pass through unchanged.
/// </summary>
public sealed class HealthcarePhiTrustGate : IDocumentTrustGate
{
    public const string ConsentField = "consent_acknowledged";

    public DocumentTrustDecision Evaluate(IDocumentTemplate template, JsonElement data)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (!string.Equals(template.Industry, "healthcare", StringComparison.OrdinalIgnoreCase))
            return DocumentTrustDecision.Allow();

        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty(ConsentField, out var consent) &&
            consent.ValueKind == JsonValueKind.True)
        {
            return DocumentTrustDecision.Allow();
        }

        return DocumentTrustDecision.Deny(
            $"Template '{template.Id}' contains Protected Health Information (PHI). " +
            $"Set \"{ConsentField}\": true in the data object to acknowledge HIPAA handling before generating.");
    }
}
