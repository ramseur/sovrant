using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Tests;

/// <summary>
/// Tests for <see cref="TemplateMatcher"/>. Uses a mix of DB-backed stubs (the 42
/// Scriban templates seeded by V037) and C# Excel templates. The matcher only reads
/// Id, Name, Industry, and Description so lightweight stubs work fine.
/// </summary>
public class TemplateMatcherTests
{
    [Fact]
    public void Routes_nda_prompt_to_legal_nda_template()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "draft me an NDA between Acme and Globex");

        Assert.NotEmpty(matches);
        Assert.Equal("legal/nda", matches[0].Template.Id);
    }

    [Fact]
    public void Routes_invoice_prompt_to_finance_invoice()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "generate an invoice for consulting hours");

        Assert.Equal("finance/invoice", matches[0].Template.Id);
    }

    [Fact]
    public void Routes_meeting_notes_prompt_to_business_template()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "capture meeting notes from today's standup");

        Assert.Equal("business/meeting-notes", matches[0].Template.Id);
    }

    [Fact]
    public void Returns_empty_for_empty_prompt()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "");
        Assert.Empty(matches);
    }

    [Fact]
    public void Returns_empty_when_no_meaningful_tokens_match()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "foo bar xyzzy qwerty");
        Assert.Empty(matches);
    }

    [Fact]
    public void Ignores_common_verbs_like_draft_and_generate()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "draft generate write produce create");
        Assert.Empty(matches);
    }

    [Fact]
    public void Synonyms_route_bill_to_invoice()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "send me a bill for last month");

        Assert.NotEmpty(matches);
        Assert.Equal("finance/invoice", matches[0].Template.Id);
    }

    [Fact]
    public void Respects_limit_parameter()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "business report plan", limit: 3);
        Assert.True(matches.Count <= 3);
    }

    [Fact]
    public void Match_includes_matched_terms_for_transparency()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "lease agreement for tenant");

        Assert.NotEmpty(matches);
        var top = matches[0];
        Assert.Equal("real-estate/lease-agreement", top.Template.Id);
        Assert.Contains("lease", top.MatchedTerms, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Hipaa_shorthand_routes_to_healthcare()
    {
        var matches = TemplateMatcher.Rank(AllTemplates(), "HIPAA authorization form for Jane Doe");

        Assert.NotEmpty(matches);
        Assert.Equal("healthcare/hipaa-authorization", matches[0].Template.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IEnumerable<IDocumentTemplate> AllTemplates() =>
    [
        // 42 DB-backed templates (kind=document-templates, seeded by V037)
        Stub("business/meeting-notes",        "Meeting Notes",                    "business",     "Structured meeting notes with attendees, agenda, decisions, and action items."),
        Stub("business/sow",                  "Statement of Work",                "business",     "Statement of Work with scope, deliverables, milestones, and payment terms."),
        Stub("business/project-status",       "Project Status Report",            "business",     "Project status report with RAG health, milestones, and risks."),
        Stub("business/employee-offer",       "Employment Offer Letter",          "business",     "Employment offer letter with compensation, benefits, and start date."),
        Stub("business/performance-review",   "Performance Review",               "business",     "Employee performance review with competency ratings and goals."),
        Stub("business/business-plan",        "Business Plan",                    "business",     "Comprehensive business plan with executive summary and financial projections."),
        Stub("construction/bid-proposal",     "Bid Proposal",                     "construction", "Construction bid proposal with line items, overhead, and total price."),
        Stub("construction/change-order",     "Change Order",                     "construction", "Construction change order with updated contract amounts."),
        Stub("construction/daily-log",        "Daily Construction Log",           "construction", "Daily construction log with crews, weather, and work performed."),
        Stub("construction/punch-list",       "Punch List",                       "construction", "Construction punch list with items, trades, and status."),
        Stub("construction/safety-report",    "Safety Incident Report",           "construction", "Safety incident report with narrative and corrective actions."),
        Stub("education/iep",                 "Individualized Education Program", "education",    "IEP with present levels, annual goals, services, and team."),
        Stub("education/lesson-plan",         "Lesson Plan",                      "education",    "Lesson plan with objectives, materials, and procedure."),
        Stub("education/report-card",         "Report Card",                      "education",    "Student report card with subject grades and attendance."),
        Stub("education/syllabus",            "Course Syllabus",                  "education",    "Course syllabus with objectives, grading breakdown, and schedule."),
        Stub("education/transcript",          "Academic Transcript",              "education",    "Official academic transcript with term-by-term courses and GPA."),
        Stub("finance/invoice",               "Invoice",                          "finance",      "Professional invoice with line items, tax, and totals."),
        Stub("finance/budget-report",         "Budget Report",                    "finance",      "Budget vs. actuals report with per-category variance."),
        Stub("finance/audit-report",          "Audit Report",                     "finance",      "Audit report with findings grouped by severity."),
        Stub("finance/financial-statement",   "Financial Statement",              "finance",      "Financial statement with income statement, balance sheet, and cash flow."),
        Stub("finance/proposal",              "Business Proposal",                "finance",      "Business proposal with pricing, timeline, and acceptance."),
        Stub("healthcare/care-plan",          "Care Plan",                        "healthcare",   "Patient care plan with diagnoses, problems, goals, and interventions."),
        Stub("healthcare/discharge-summary",  "Discharge Summary",                "healthcare",   "Discharge summary with hospital course and follow-up instructions."),
        Stub("healthcare/hipaa-authorization","HIPAA Authorization",              "healthcare",   "HIPAA authorization for release of health information."),
        Stub("healthcare/patient-intake",     "Patient Intake Form",              "healthcare",   "Patient intake form with demographics, history, and chief complaint."),
        Stub("healthcare/progress-note",      "Progress Note (SOAP)",             "healthcare",   "SOAP progress note with subjective, objective, assessment, and plan."),
        Stub("healthcare/referral-letter",    "Referral Letter",                  "healthcare",   "Physician referral letter with clinical summary and urgency."),
        Stub("healthcare/superbill",          "Superbill",                        "healthcare",   "Medical superbill with diagnoses, CPT codes, and charges."),
        Stub("legal/nda",                     "Non-Disclosure Agreement",         "legal",        "Mutual NDA with parties, term, scope, and governing law."),
        Stub("legal/service-agreement",       "Service Agreement",                "legal",        "Service agreement with scope, fees, term, and governing law."),
        Stub("legal/engagement-letter",       "Engagement Letter",                "legal",        "Legal engagement letter with scope, fees, and retainer."),
        Stub("legal/demand-letter",           "Demand Letter",                    "legal",        "Demand letter with facts, legal basis, and response deadline."),
        Stub("legal/corporate-minutes",       "Corporate Meeting Minutes",        "legal",        "Corporate meeting minutes with attendees, agenda, and resolutions."),
        Stub("legal/power-of-attorney",       "Power of Attorney",                "legal",        "Power of attorney with principal, agent, and granted powers."),
        Stub("legal/terms-of-service",        "Terms of Service",                 "legal",        "Terms of service with acceptable use, disclaimers, and governing law."),
        Stub("real-estate/property-listing",  "Property Listing",                 "real-estate",  "Property listing with details, amenities, and agent contact."),
        Stub("real-estate/purchase-agreement","Purchase Agreement",               "real-estate",  "Real estate purchase agreement with price, contingencies, and closing."),
        Stub("real-estate/lease-agreement",   "Lease Agreement",                  "real-estate",  "Residential lease agreement with rent, deposit, and house rules."),
        Stub("real-estate/cma-report",        "Comparative Market Analysis",      "real-estate",  "CMA report with comparable sales and recommended price range."),
        Stub("real-estate/closing-disclosure","Closing Disclosure",               "real-estate",  "Closing disclosure with loan terms and itemized closing costs."),
        Stub("real-estate/property-inspection","Property Inspection Report",      "real-estate",  "Property inspection report with sections, findings, and severity."),
        Stub("real-estate/rental-application","Rental Application",               "real-estate",  "Rental application with applicant, employment, and references."),

        // 2 C# Excel templates (remain as C# — they emit structured JSON)
        new ExpenseReportTemplate(),
        new LoanAmortizationTemplate(),
    ];

    private static DbDocumentTemplate Stub(string slug, string name, string industry, string description)
    {
        var now = DateTimeOffset.UtcNow;
        var page = new KnowledgePage(
            KnowledgeId:   $"doctempl_{slug.Replace('/', '-')}",
            Kind:          "document-templates",
            Slug:          slug,
            Name:          name,
            Description:   description,
            Tier:          "BuiltIn",
            Body:          "stub",
            WorkspaceId:   string.Empty,
            CreatedAt:     now,
            UpdatedAt:     now,
            Industry:      industry,
            DefaultFormat: "Word");
        return new DbDocumentTemplate(page);
    }
}
