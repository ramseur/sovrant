using System.Text.Json;
using Sovrant.Runtime.Documents.Templates.Healthcare;
using Sovrant.Runtime.Documents.Templates.Legal;
using Sovrant.Runtime.Documents.Trust;

namespace Sovrant.Runtime.Documents.Tests;

public class HealthcarePhiTrustGateTests
{
    private readonly HealthcarePhiTrustGate _gate = new();

    [Fact]
    public void Allows_NonHealthcare_TemplateAlways()
    {
        var template = new NdaTemplate();
        var data = JsonDocument.Parse("{}").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.DenyReason);
    }

    [Fact]
    public void Denies_Healthcare_WithoutConsent()
    {
        var template = new PatientIntakeTemplate();
        var data = JsonDocument.Parse("""{ "patient_name": "Jane" }""").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.False(decision.IsAllowed);
        Assert.Contains("PHI", decision.DenyReason!);
        Assert.Contains(HealthcarePhiTrustGate.ConsentField, decision.DenyReason);
    }

    [Fact]
    public void Denies_Healthcare_WhenConsentIsFalse()
    {
        var template = new PatientIntakeTemplate();
        var data = JsonDocument.Parse("""{ "consent_acknowledged": false }""").RootElement;

        var decision = _gate.Evaluate(template, data);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Allows_Healthcare_WhenConsentIsTrue()
    {
        var template = new PatientIntakeTemplate();
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

    private sealed class HealthcareLikeTemplate : Sovrant.Runtime.Documents.Templates.IDocumentTemplate
    {
        public HealthcareLikeTemplate(string industry) => Industry = industry;
        public string Id => "stub/test";
        public string Name => "stub";
        public string Industry { get; }
        public string Description => "";
        public DocumentFormat DefaultFormat => DocumentFormat.Markdown;
        public IReadOnlyList<Sovrant.Runtime.Documents.Templates.TemplateField> Fields { get; } = new List<Sovrant.Runtime.Documents.Templates.TemplateField>();
        public Sovrant.Runtime.Documents.Templates.TemplateRenderResult Render(JsonElement data) =>
            new(DocumentFormat.Markdown, "", "stub.md");
    }
}
