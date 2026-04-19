using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Business;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Templates.Legal;
using Sovrant.Runtime.Documents.Templates.RealEstate;

namespace Sovrant.Runtime.Documents.Tests;

public class TemplateTests : IDisposable
{
    private readonly string _artifactRoot;
    private readonly LocalArtifactStore _store;

    public TemplateTests()
    {
        _artifactRoot = Path.Combine(Path.GetTempPath(), "sovrant-template-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_artifactRoot);
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactRoot);
    }

    [Fact]
    public void Registry_rejects_duplicate_ids()
    {
        var first = new InvoiceTemplate();
        var second = new InvoiceTemplate();
        Assert.Throws<InvalidOperationException>(() =>
            new TemplateRegistry(new IDocumentTemplate[] { first, second }));
    }

    [Fact]
    public void Registry_filters_by_industry_and_search()
    {
        var registry = new TemplateRegistry(AllTemplates());

        var legal = registry.Find(industry: "legal").ToList();
        Assert.Single(legal);
        Assert.Equal("legal/nda", legal[0].Id);

        var invoiceHits = registry.Find(search: "invoice").ToList();
        Assert.Contains(invoiceHits, t => t.Id == "finance/invoice");
    }

    [Fact]
    public void Template_data_rejects_missing_required_field()
    {
        var fields = new List<TemplateField>
        {
            new() { Name = "a", Type = TemplateFieldType.String, Required = true },
        };
        using var doc = JsonDocument.Parse("{}");
        Assert.Throws<TemplateValidationException>(() => TemplateData.Validate(doc.RootElement, fields));
    }

    [Fact]
    public void Template_data_rejects_wrong_type()
    {
        var fields = new List<TemplateField>
        {
            new() { Name = "n", Type = TemplateFieldType.Integer, Required = true },
        };
        using var doc = JsonDocument.Parse("""{"n":"not a number"}""");
        Assert.Throws<TemplateValidationException>(() => TemplateData.Validate(doc.RootElement, fields));
    }

    [Fact]
    public async Task Invoice_template_renders_to_structured_pdf()
    {
        var template = new InvoiceTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "invoice_number": "INV-2026-001",
            "issue_date": "2026-04-17",
            "due_date": "2026-05-17",
            "currency": "USD",
            "seller": "Acme Corp\n123 Main St",
            "buyer": "Beta LLC\n456 Market St",
            "line_items": [
                {"description":"Widget","quantity":2,"unit_price":19.99},
                {"description":"Gadget","quantity":1,"unit_price":49.50}
            ],
            "tax_rate": 8.25,
            "payment_terms": "Net 30"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("INV-2026-001", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("| Description | Qty | Unit price | Subtotal |", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var request = BuildRequest(rendered);
        var result = await generator.GenerateAsync(request);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.SizeBytes > 500);
    }

    [Fact]
    public async Task Meeting_notes_template_renders_to_word()
    {
        var template = new MeetingNotesTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "title": "Q2 Planning",
            "meeting_date": "2026-04-17",
            "location": "Conference Room A",
            "attendees": ["Alice","Bob","Carol"],
            "agenda": ["Review roadmap","Resource planning"],
            "discussion": "Covered Q2 priorities.",
            "decisions": ["Ship feature X in May"],
            "action_items": [
                {"task":"Draft spec","owner":"Alice","due_date":"2026-04-24"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Nda_template_renders_to_word_with_parties()
    {
        var template = new NdaTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "party_a": "Acme Corp",
            "party_b": "Beta LLC",
            "effective_date": "2026-04-17",
            "term_years": 3,
            "purpose": "Evaluate potential partnership.",
            "governing_law": "State of Delaware"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Acme Corp", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("3 years", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Expense_report_template_renders_to_excel()
    {
        var template = new ExpenseReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "employee_name": "Jane Doe",
            "employee_id": "E-42",
            "department": "Engineering",
            "period_start": "2026-04-01",
            "period_end": "2026-04-15",
            "currency": "USD",
            "purpose": "Conference travel",
            "expenses": [
                {"date":"2026-04-02","category":"Flight","description":"SFO to JFK","amount":450},
                {"date":"2026-04-03","category":"Hotel","description":"2 nights","amount":300}
            ],
            "tax_rate": 0
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Excel, rendered.Format);

        using var bodyDoc = JsonDocument.Parse(rendered.Body);
        Assert.True(bodyDoc.RootElement.TryGetProperty("preamble", out _));
        Assert.True(bodyDoc.RootElement.TryGetProperty("headers", out _));
        Assert.True(bodyDoc.RootElement.TryGetProperty("rows", out _));

        var generator = new ExcelDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Property_listing_template_renders_to_structured_pdf()
    {
        var template = new PropertyListingTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "headline": "Charming Craftsman in Noe Valley",
            "address": "123 Elm St\nSan Francisco, CA 94114",
            "price": 1850000,
            "bedrooms": 3,
            "bathrooms": 2.5,
            "square_feet": 2100,
            "year_built": 1912,
            "property_type": "Single-family",
            "amenities": ["Renovated kitchen","Private garden"],
            "description": "A lovingly restored home in the heart of Noe Valley.",
            "agent_name": "Pat Realtor",
            "agent_email": "pat@example.com"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Charming Craftsman", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Financial_statement_template_renders_to_structured_pdf()
    {
        var template = new FinancialStatementTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "entity_name": "Acme Corp",
            "period": "Q1 2026",
            "income_statement": [
                {"label":"Revenue","amount":250000},
                {"label":"COGS","amount":-100000},
                {"label":"Operating expenses","amount":-60000}
            ],
            "balance_sheet": [
                {"label":"Cash","amount":50000,"section":"Assets"},
                {"label":"Accounts receivable","amount":25000,"section":"Assets"},
                {"label":"Accounts payable","amount":15000,"section":"Liabilities"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Income Statement", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Assets", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Budget_report_template_renders_with_variance()
    {
        var template = new BudgetReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "owner": "Engineering",
            "period": "April 2026",
            "categories": [
                {"category":"Salaries","planned":100000,"actual":98000},
                {"category":"Software","planned":10000,"actual":12500}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Contains("Variance", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Loan_amortization_template_renders_to_excel()
    {
        var template = new LoanAmortizationTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "borrower": "Jane Doe",
            "loan_name": "5-year auto loan",
            "principal": 25000,
            "annual_rate_percent": 6.5,
            "term_months": 60,
            "first_payment_date": "2026-05-01"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Excel, rendered.Format);

        using var body = JsonDocument.Parse(rendered.Body);
        Assert.True(body.RootElement.TryGetProperty("rows", out var rows));
        Assert.True(rows.GetArrayLength() > 60);

        var generator = new ExcelDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
    }

    [Fact]
    public async Task Audit_report_template_groups_findings_by_severity()
    {
        var template = new AuditReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "entity_name": "Acme Corp",
            "audit_type": "Internal Controls",
            "period": "FY 2025",
            "auditor": "Internal Audit Team",
            "report_date": "2026-04-17",
            "scope": "Review of financial reporting controls.",
            "findings": [
                {"title":"Segregation of duties","severity":"High","description":"Same person creates and approves journal entries."},
                {"title":"Vendor master hygiene","severity":"Medium","description":"Stale vendors not deactivated."},
                {"title":"Documentation","severity":"Low","description":"Some reconciliations lack sign-off."}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Contains("Severity: High", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Segregation of duties", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Proposal_template_renders_pricing_and_acceptance()
    {
        var template = new ProposalTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "proposal_title": "Website Redesign",
            "prepared_for": "Beta LLC",
            "prepared_by": "Acme Design Studio",
            "proposal_date": "2026-04-18",
            "executive_summary": "Full redesign of the Beta LLC marketing site.",
            "scope": "Information architecture, visual design, and front-end build.",
            "deliverables": ["Wireframes","High-fidelity mockups","Production site"],
            "pricing": [
                {"item":"Discovery","quantity":1,"unit_price":5000},
                {"item":"Design","quantity":1,"unit_price":25000},
                {"item":"Build","quantity":1,"unit_price":40000}
            ],
            "timeline": [
                {"milestone":"Kickoff","target_date":"2026-05-01"},
                {"milestone":"Final delivery","target_date":"2026-08-30"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Contains("Acceptance", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Pricing", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Sow_template_renders_to_word_with_signatures()
    {
        var template = new StatementOfWorkTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "sow_number": "SOW-2026-014",
            "effective_date": "2026-04-20",
            "client_name": "Beta LLC",
            "vendor_name": "Acme Consulting",
            "scope": "Implementation of analytics dashboard.",
            "deliverables": [
                {"name":"Discovery report","acceptance_criteria":"Approved by sponsor."},
                {"name":"Production rollout","acceptance_criteria":"Pilot users sign off."}
            ],
            "milestones": [
                {"name":"Discovery complete","target_date":"2026-05-15","payment_percent":25},
                {"name":"Final delivery","target_date":"2026-08-30","payment_percent":75}
            ],
            "total_fee": 80000,
            "currency": "USD"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("SOW-2026-014", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Signatures", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Project_status_template_renders_milestones_and_risks()
    {
        var template = new ProjectStatusReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "project_name": "Atlas",
            "period": "Week of 2026-04-13",
            "project_manager": "Alice",
            "overall_health": "Yellow",
            "summary": "Schedule slipped two days due to vendor delay.",
            "milestones": [
                {"name":"API freeze","status":"At Risk","target_date":"2026-04-30","notes":"Awaiting vendor SDK."}
            ],
            "accomplishments": ["Closed 12 issues"],
            "planned_next_period": ["Resume integration testing"],
            "risks": [
                {"title":"Vendor SDK delay","severity":"High","mitigation":"Escalate to account manager."}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Yellow", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Vendor SDK delay", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Offer_letter_template_renders_with_compensation()
    {
        var template = new EmployeeOfferLetterTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "company_name": "Acme Corp",
            "candidate_name": "Sam Hire",
            "position_title": "Senior Engineer",
            "department": "Platform",
            "start_date": "2026-05-12",
            "base_salary": 175000,
            "salary_frequency": "annual",
            "currency": "USD",
            "sign_on_bonus": 15000,
            "target_bonus_percent": 15,
            "contingencies": ["Background check","Work authorization"],
            "signer_name": "Pat Hiring",
            "signer_title": "VP Engineering",
            "letter_date": "2026-04-17"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Senior Engineer", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Sign-on bonus", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("at-will", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Performance_review_template_renders_competencies_and_goals()
    {
        var template = new PerformanceReviewTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "employee_name": "Jordan Dev",
            "employee_title": "Engineer II",
            "reviewer_name": "Alex Manager",
            "review_period": "FY2025",
            "review_date": "2026-04-17",
            "overall_rating": "Exceeds",
            "summary": "Strong year with significant impact.",
            "competencies": [
                {"name":"Technical depth","rating":"Exceeds","comments":"Led the migration to .NET 10."},
                {"name":"Collaboration","rating":"Meets"}
            ],
            "accomplishments": ["Shipped feature X","Mentored two new hires"],
            "goals_next_period": [
                {"goal":"Lead architecture council","target_date":"2026-12-31","success_criteria":"Quarterly arch review on time."}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Competency Ratings", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Goals for Next Period", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Business_plan_template_renders_with_financial_projections()
    {
        var template = new BusinessPlanTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "company_name": "Nimbus Robotics",
            "plan_date": "2026-04-17",
            "currency": "USD",
            "executive_summary": "Nimbus Robotics designs warehouse automation.",
            "company_overview": "Founded 2024 in San Francisco.",
            "products_services": "Autonomous mobile robots and fleet software.",
            "market_analysis": "Warehouse automation is a $30B market growing 15% annually.",
            "competitors": ["Locus","6 River Systems"],
            "management_team": [
                {"name":"Sam Founder","role":"CEO","background":"15 years robotics."}
            ],
            "financial_projections": [
                {"year":"2026","revenue":2000000,"expenses":3500000,"net_income":-1500000},
                {"year":"2027","revenue":8000000,"expenses":7000000,"net_income":1000000}
            ],
            "funding_request": "Seeking $10M Series A."
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Financial Projections", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Nimbus Robotics", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    private static IEnumerable<IDocumentTemplate> AllTemplates() => new IDocumentTemplate[]
    {
        new InvoiceTemplate(),
        new MeetingNotesTemplate(),
        new NdaTemplate(),
        new ExpenseReportTemplate(),
        new PropertyListingTemplate(),
        new FinancialStatementTemplate(),
        new BudgetReportTemplate(),
        new LoanAmortizationTemplate(),
        new AuditReportTemplate(),
        new ProposalTemplate(),
        new StatementOfWorkTemplate(),
        new ProjectStatusReportTemplate(),
        new EmployeeOfferLetterTemplate(),
        new PerformanceReviewTemplate(),
        new BusinessPlanTemplate(),
    };

    private static DocumentRequest BuildRequest(TemplateRenderResult rendered) => new()
    {
        Format = rendered.Format,
        Body = rendered.Body,
        FileName = rendered.DefaultFileName,
        Title = rendered.Title,
        Scope = new ArtifactScope
        {
            WorkspaceId = ArtifactScope.DefaultWorkspaceId,
            ProjectId = ArtifactScope.DefaultProjectId,
            RunId = $"test-{Guid.NewGuid():N}",
        },
    };

    private async Task<bool> IsZipArchiveAsync(DocumentResult result)
    {
        using var stream = await _store.ReadAsync(result.Handle, result.RelativePath);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;
        try
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            return zip.Entries.Count > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_artifactRoot)) Directory.Delete(_artifactRoot, recursive: true); }
        catch (IOException) { /* best effort */ }
    }
}
