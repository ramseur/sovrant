using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Packages;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Business;
using Sovrant.Runtime.Documents.Templates.Construction;
using Sovrant.Runtime.Documents.Templates.Education;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Templates.Healthcare;
using Sovrant.Runtime.Documents.Templates.Legal;
using Sovrant.Runtime.Documents.Templates.RealEstate;
using Sovrant.Runtime.Documents.Trust;

namespace Sovrant.Runtime.Documents;

/// <summary>DI wiring for the document generation subsystem.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default set of document generators and the registry.
    /// Consumers (CLI, Desktop, Web, Server, MCP) get document generation
    /// just by calling this after <c>AddSovrantRuntime</c>.
    /// </summary>
    public static IServiceCollection AddSovrantDocuments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDocumentGenerator, MarkdownGenerator>();
        services.AddSingleton<IDocumentGenerator, PdfSharpGenerator>();
        services.AddSingleton<IDocumentGenerator, MigraDocGenerator>();
        services.AddSingleton<IDocumentGenerator, WordDocumentGenerator>();
        services.AddSingleton<IDocumentGenerator, ExcelDocumentGenerator>();
        services.AddSingleton<IDocumentGenerator, PowerPointGenerator>();

        services.AddSingleton<IDocumentGeneratorRegistry, DocumentGeneratorRegistry>();

        // Business
        services.AddSingleton<IDocumentTemplate, MeetingNotesTemplate>();
        services.AddSingleton<IDocumentTemplate, StatementOfWorkTemplate>();
        services.AddSingleton<IDocumentTemplate, ProjectStatusReportTemplate>();
        services.AddSingleton<IDocumentTemplate, EmployeeOfferLetterTemplate>();
        services.AddSingleton<IDocumentTemplate, PerformanceReviewTemplate>();
        services.AddSingleton<IDocumentTemplate, BusinessPlanTemplate>();

        // Finance
        services.AddSingleton<IDocumentTemplate, InvoiceTemplate>();
        services.AddSingleton<IDocumentTemplate, ExpenseReportTemplate>();
        services.AddSingleton<IDocumentTemplate, FinancialStatementTemplate>();
        services.AddSingleton<IDocumentTemplate, BudgetReportTemplate>();
        services.AddSingleton<IDocumentTemplate, LoanAmortizationTemplate>();
        services.AddSingleton<IDocumentTemplate, AuditReportTemplate>();
        services.AddSingleton<IDocumentTemplate, ProposalTemplate>();

        // Legal
        services.AddSingleton<IDocumentTemplate, NdaTemplate>();
        services.AddSingleton<IDocumentTemplate, ServiceAgreementTemplate>();
        services.AddSingleton<IDocumentTemplate, EngagementLetterTemplate>();
        services.AddSingleton<IDocumentTemplate, DemandLetterTemplate>();
        services.AddSingleton<IDocumentTemplate, CorporateMinutesTemplate>();
        services.AddSingleton<IDocumentTemplate, PowerOfAttorneyTemplate>();
        services.AddSingleton<IDocumentTemplate, TermsOfServiceTemplate>();

        // Real estate
        services.AddSingleton<IDocumentTemplate, PropertyListingTemplate>();
        services.AddSingleton<IDocumentTemplate, PurchaseAgreementTemplate>();
        services.AddSingleton<IDocumentTemplate, LeaseAgreementTemplate>();
        services.AddSingleton<IDocumentTemplate, CmaReportTemplate>();
        services.AddSingleton<IDocumentTemplate, ClosingDisclosureTemplate>();
        services.AddSingleton<IDocumentTemplate, PropertyInspectionTemplate>();
        services.AddSingleton<IDocumentTemplate, RentalApplicationTemplate>();

        // Healthcare
        services.AddSingleton<IDocumentTemplate, PatientIntakeTemplate>();
        services.AddSingleton<IDocumentTemplate, CarePlanTemplate>();
        services.AddSingleton<IDocumentTemplate, DischargeSummaryTemplate>();
        services.AddSingleton<IDocumentTemplate, HipaaAuthorizationTemplate>();
        services.AddSingleton<IDocumentTemplate, SuperbillTemplate>();
        services.AddSingleton<IDocumentTemplate, ProgressNoteTemplate>();
        services.AddSingleton<IDocumentTemplate, ReferralLetterTemplate>();

        // Education
        services.AddSingleton<IDocumentTemplate, SyllabusTemplate>();
        services.AddSingleton<IDocumentTemplate, LessonPlanTemplate>();
        services.AddSingleton<IDocumentTemplate, ReportCardTemplate>();
        services.AddSingleton<IDocumentTemplate, IepTemplate>();
        services.AddSingleton<IDocumentTemplate, TranscriptTemplate>();

        // Construction
        services.AddSingleton<IDocumentTemplate, BidProposalTemplate>();
        services.AddSingleton<IDocumentTemplate, ChangeOrderTemplate>();
        services.AddSingleton<IDocumentTemplate, DailyLogTemplate>();
        services.AddSingleton<IDocumentTemplate, PunchListTemplate>();
        services.AddSingleton<IDocumentTemplate, SafetyReportTemplate>();

        services.AddSingleton<ITemplateRegistry, TemplateRegistry>();

        // User-authored document templates — DB-backed (Phase 112C). Coexists with the
        // code-defined IDocumentTemplate registry above; the Knowledge UI renders both.
        services.AddSingleton<UserDocumentTemplateRegistry>(sp =>
            new UserDocumentTemplateRegistry(
                sp.GetRequiredService<Sovrant.Runtime.Knowledge.IKnowledgeStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UserDocumentTemplateRegistry>>()));

        // Document packages — bundles of templates rendered against shared data.
        foreach (var pkg in BuiltInDocumentPackages.All)
            services.AddSingleton(pkg);
        services.AddSingleton<IDocumentPackageRegistry>(sp =>
            new DocumentPackageRegistry(sp.GetServices<DocumentPackage>()));

        // Default trust gate — refuses healthcare/* templates without
        // explicit PHI consent. Apps wanting different policy register
        // their own IDocumentTrustGate before calling AddSovrantDocuments
        // (TryAddSingleton honors existing registration).
        services.TryAddSingleton<IDocumentTrustGate, HealthcarePhiTrustGate>();

        return services;
    }
}
