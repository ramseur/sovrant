using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Business;
using Sovrant.Runtime.Documents.Templates.Construction;
using Sovrant.Runtime.Documents.Templates.Education;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Templates.Healthcare;
using Sovrant.Runtime.Documents.Templates.Legal;
using Sovrant.Runtime.Documents.Templates.RealEstate;

namespace Sovrant.Runtime.Documents.Tests;

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
        // "draft" and "generate" are stopwords — they should not boost legal/nda or finance/invoice
        // over more specific matches.
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

    private static IEnumerable<IDocumentTemplate> AllTemplates() =>
    [
        new MeetingNotesTemplate(),
        new StatementOfWorkTemplate(),
        new ProjectStatusReportTemplate(),
        new EmployeeOfferLetterTemplate(),
        new PerformanceReviewTemplate(),
        new BusinessPlanTemplate(),
        new InvoiceTemplate(),
        new ExpenseReportTemplate(),
        new FinancialStatementTemplate(),
        new BudgetReportTemplate(),
        new LoanAmortizationTemplate(),
        new AuditReportTemplate(),
        new ProposalTemplate(),
        new NdaTemplate(),
        new ServiceAgreementTemplate(),
        new EngagementLetterTemplate(),
        new DemandLetterTemplate(),
        new CorporateMinutesTemplate(),
        new PowerOfAttorneyTemplate(),
        new TermsOfServiceTemplate(),
        new PropertyListingTemplate(),
        new PurchaseAgreementTemplate(),
        new LeaseAgreementTemplate(),
        new CmaReportTemplate(),
        new ClosingDisclosureTemplate(),
        new PropertyInspectionTemplate(),
        new RentalApplicationTemplate(),
        new PatientIntakeTemplate(),
        new CarePlanTemplate(),
        new DischargeSummaryTemplate(),
        new HipaaAuthorizationTemplate(),
        new SuperbillTemplate(),
        new ProgressNoteTemplate(),
        new ReferralLetterTemplate(),
        new SyllabusTemplate(),
        new LessonPlanTemplate(),
        new ReportCardTemplate(),
        new IepTemplate(),
        new TranscriptTemplate(),
        new BidProposalTemplate(),
        new ChangeOrderTemplate(),
        new DailyLogTemplate(),
        new PunchListTemplate(),
        new SafetyReportTemplate(),
    ];
}
