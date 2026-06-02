using System.Text.Json;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Trust;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Tests;

public class HealthcarePhiTrustGateTests
{
    private readonly HealthcarePhiTrustGate _gate = new();

    [Fact]
    public void Allows_NonHealthcare_TemplateAlways()
    {
        // legal/nda is now DB-backed — use a stub with the right industry
        var template = MakeDbTemplate("legal/nda", "legal");
        var data = JsonDocument.Parse("{}").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.DenyReason);
    }

    [Fact]
    public void Denies_Healthcare_WithoutConsent()
    {
        var template = MakeDbTemplate("healthcare/patient-intake", "healthcare");
        var data = JsonDocument.Parse("""{ "patient_name": "Jane" }""").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.False(decision.IsAllowed);
        Assert.Contains("PHI", decision.DenyReason!);
        Assert.Contains(HealthcarePhiTrustGate.ConsentField, decision.DenyReason);
    }

    [Fact]
    public void Denies_Healthcare_WhenConsentIsFalse()
    {
        var template = MakeDbTemplate("healthcare/patient-intake", "healthcare");
        var data = JsonDocument.Parse("""{ "consent_acknowledged": false }""").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Allows_Healthcare_WhenConsentIsTrue()
    {
        var template = MakeDbTemplate("healthcare/patient-intake", "healthcare");
        var data = JsonDocument.Parse("""{ "consent_acknowledged": true }""").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void Industry_Match_IsCaseInsensitive()
    {
        var template = new HealthcareLikeTemplate("HEALTHCARE");
        var data = JsonDocument.Parse("{}").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.False(decision.IsAllowed);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DbDocumentTemplate MakeDbTemplate(string slug, string industry)
    {
        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:   $"doctempl_{slug.Replace('/', '-')}",
            Kind:          "document-templates",
            Slug:          slug,
            Name:          slug,
            Description:   string.Empty,
            Tier:          "BuiltIn",
            Body:          "stub",
            WorkspaceId:   string.Empty,
            CreatedAt:     now,
            UpdatedAt:     now,
            Industry:      industry,
            DefaultFormat: "Word");
        return new DbDocumentTemplate(page);
    }

    private sealed class HealthcareLikeTemplate : IDocumentTemplate
    {
        public HealthcareLikeTemplate(string industry) => Industry = industry;
        public string Id => "stub/test";
        public string Name => "stub";
        public string Industry { get; }
        public string Description => "";
        public DocumentFormat DefaultFormat => DocumentFormat.Markdown;
        public IReadOnlyList<TemplateField> Fields { get; } = [];
        public TemplateRenderResult Render(JsonElement data) =>
            new(DocumentFormat.Markdown, "", "stub.md");
    }
}
