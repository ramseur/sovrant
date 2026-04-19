using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Business;
using Sovrant.Runtime.Documents.Templates.Construction;
using Sovrant.Runtime.Documents.Templates.Education;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Documents.Templates.Healthcare;
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
        Assert.Contains(legal, t => t.Id == "legal/nda");
        Assert.All(legal, t => Assert.Equal("legal", t.Industry));

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

    [Fact]
    public async Task Service_agreement_template_renders_with_boilerplate()
    {
        var template = new ServiceAgreementTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "client_name": "Beta LLC",
            "provider_name": "Acme Consulting",
            "effective_date": "2026-04-20",
            "services_description": "Consulting on analytics strategy.",
            "fees": "Hourly at $250/hr, billed monthly.",
            "payment_terms": "Net 30",
            "term": "1 year, auto-renewing",
            "governing_law": "State of Delaware"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Limitation of Liability", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Delaware", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Engagement_letter_template_renders_with_retainer()
    {
        var template = new EngagementLetterTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "firm_name": "Smith & Jones LLP",
            "client_name": "Beta LLC",
            "letter_date": "2026-04-19",
            "matter_description": "Review of vendor contracts.",
            "scope_of_services": "Negotiation support and redlines.",
            "fee_structure": "Hourly at attorney rates ranging $350-$600/hr.",
            "retainer_amount": 5000,
            "currency": "USD",
            "billing_cycle": "monthly",
            "signer_name": "Alex Smith",
            "signer_title": "Partner"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("retainer", rendered.Body, StringComparison.OrdinalIgnoreCase);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Demand_letter_template_renders_with_deadline()
    {
        var template = new DemandLetterTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "sender_name": "Pat Plaintiff",
            "recipient_name": "Devin Debtor",
            "letter_date": "2026-04-19",
            "subject": "Unpaid Invoice #4821",
            "facts": "Invoice #4821 for $12,500 was issued 2026-01-15 and remains unpaid.",
            "demand": "Payment of the outstanding balance in full.",
            "amount_due": 12500,
            "response_deadline": "2026-05-05"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("2026-05-05", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Unpaid Invoice", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Corporate_minutes_template_renders_resolutions()
    {
        var template = new CorporateMinutesTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "company_name": "Acme Corp",
            "meeting_type": "Board of Directors",
            "meeting_date": "2026-04-10",
            "meeting_time": "10:00 AM",
            "location": "Company HQ",
            "chair": "Chris Chair",
            "secretary": "Sara Secretary",
            "attendees_present": ["Chris Chair","Sara Secretary","Drew Director"],
            "quorum_established": true,
            "agenda": ["Approve minutes","Budget ratification"],
            "resolutions": [
                {"title":"Ratify FY26 budget","text":"RESOLVED, the FY26 budget is approved as presented.","moved_by":"Drew Director","seconded_by":"Sara Secretary","votes_for":3,"votes_against":0,"votes_abstain":0,"outcome":"Passed"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Ratify FY26 budget", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Passed", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Power_of_attorney_template_renders_with_powers()
    {
        var template = new PowerOfAttorneyTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "principal_name": "Mia Principal",
            "agent_name": "Avi Agent",
            "poa_type": "Durable",
            "effective_date": "2026-04-19",
            "is_durable": true,
            "powers_granted": [
                "Manage real property transactions",
                "File tax returns",
                "Access bank accounts"
            ],
            "governing_state": "California"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("attorney-in-fact", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Manage real property transactions", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("shall not be affected", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Terms_of_service_template_renders_standard_sections()
    {
        var template = new TermsOfServiceTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "company_name": "Nimbus Inc",
            "service_name": "Nimbus Cloud",
            "website_url": "https://nimbus.example",
            "effective_date": "2026-04-19",
            "contact_email": "legal@nimbus.example",
            "service_description": "Nimbus Cloud is a hosted file-sync service.",
            "acceptable_use": ["Upload malware","Impersonate others","Violate law"],
            "governing_law": "State of California"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Limitation of Liability", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Acceptable Use", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Purchase_agreement_template_renders_with_contingencies()
    {
        var template = new PurchaseAgreementTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "buyer_name": "Brooke Buyer",
            "seller_name": "Sally Seller",
            "property_address": "123 Oak St\nSan Jose, CA 95112",
            "purchase_price": 1200000,
            "earnest_money": 36000,
            "currency": "USD",
            "financing_contingency": true,
            "inspection_contingency": true,
            "appraisal_contingency": false,
            "closing_date": "2026-06-15",
            "governing_state": "California",
            "effective_date": "2026-04-19"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Contingencies", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("waived", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Lease_agreement_template_renders_with_rent_and_rules()
    {
        var template = new LeaseAgreementTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "landlord_name": "Logan Landlord",
            "tenant_names": ["Taylor Tenant","Jordan Tenant"],
            "premises_address": "456 Pine Ave, Apt 3\nOakland, CA 94607",
            "lease_start": "2026-05-01",
            "lease_end": "2027-04-30",
            "monthly_rent": 3200,
            "currency": "USD",
            "rent_due_day": 1,
            "security_deposit": 3200,
            "pets_allowed": false,
            "smoking_allowed": false,
            "house_rules": ["Quiet hours 10pm-7am","No sublets"],
            "governing_state": "California"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Quiet hours", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Security Deposit", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Cma_template_renders_with_comparables_and_averages()
    {
        var template = new CmaReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "prepared_for": "Sally Seller",
            "prepared_by": "Pat Realtor",
            "report_date": "2026-04-19",
            "subject_address": "123 Oak St, San Jose",
            "subject_bedrooms": 3,
            "subject_bathrooms": 2,
            "subject_square_feet": 1800,
            "comparables": [
                {"address":"100 Oak St","sold_price":1150000,"sold_date":"2026-03-15","bedrooms":3,"bathrooms":2,"square_feet":1750},
                {"address":"200 Oak St","sold_price":1250000,"sold_date":"2026-02-10","bedrooms":3,"bathrooms":2.5,"square_feet":1900}
            ],
            "suggested_price_low": 1180000,
            "suggested_price_high": 1250000
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Average sold price", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Suggested Listing Price Range", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Closing_disclosure_template_renders_loan_terms()
    {
        var template = new ClosingDisclosureTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "borrower_name": "Brooke Buyer",
            "seller_name": "Sally Seller",
            "property_address": "123 Oak St",
            "closing_date": "2026-06-15",
            "lender_name": "Big Bank",
            "loan_amount": 900000,
            "interest_rate_percent": 6.25,
            "loan_term_years": 30,
            "monthly_principal_interest": 5543,
            "purchase_price": 1200000,
            "down_payment": 300000,
            "currency": "USD",
            "closing_costs": [
                {"description":"Title insurance","amount":2500,"paid_by":"Borrower"},
                {"description":"Origination fee","amount":4500,"paid_by":"Borrower"}
            ],
            "cash_to_close": 307000
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Loan Terms", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Cash to Close", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Inspection_template_renders_sections_and_severity()
    {
        var template = new PropertyInspectionTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "property_address": "123 Oak St",
            "client_name": "Brooke Buyer",
            "inspector_name": "Ira Inspector",
            "inspection_date": "2026-04-18",
            "overall_condition": "Good",
            "sections": [
                {"name":"Roof","condition":"Good","findings":[
                    {"description":"Minor flashing wear near chimney","severity":"Minor","recommendation":"Monitor; reseal in 6 months."}
                ]},
                {"name":"Electrical","findings":[
                    {"description":"Outlet near sink is not GFCI","severity":"Safety","recommendation":"Install GFCI outlet."}
                ]}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("[Safety]", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("GFCI", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Rental_application_template_renders_applicant_and_employment()
    {
        var template = new RentalApplicationTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "property_address": "456 Pine Ave, Apt 3",
            "desired_move_in": "2026-05-01",
            "monthly_rent": 3200,
            "currency": "USD",
            "applicant_name": "Alex Applicant",
            "applicant_email": "alex@example.com",
            "applicant_phone": "555-123-4567",
            "employer_name": "Acme Corp",
            "position": "Engineer",
            "monthly_income": 9500,
            "has_pets": false
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Employment", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Authorization and Signature", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Patient_intake_template_renders_demographics_and_history()
    {
        var template = new PatientIntakeTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "practice_name": "Lakeshore Family Medicine",
            "intake_date": "2026-04-18",
            "patient_name": "Jordan Sample",
            "date_of_birth": "1988-03-14",
            "reason_for_visit": "Annual physical and medication review.",
            "current_medications": ["Atorvastatin 20mg daily", "Lisinopril 10mg daily"],
            "allergies": ["Penicillin"],
            "medical_history": ["Hypertension", "Hyperlipidemia"]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Patient Intake Form", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Penicillin", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Protected Health Information", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Care_plan_template_renders_problems_and_interventions()
    {
        var template = new CarePlanTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "patient_name": "Jordan Sample",
            "plan_date": "2026-04-18",
            "author_name": "Dr. Morgan Lee",
            "author_role": "MD",
            "diagnoses": ["Type 2 Diabetes Mellitus", "Essential Hypertension"],
            "problems": [
                {
                    "problem": "Poorly controlled A1c",
                    "goal": "A1c below 7.0 within 6 months.",
                    "interventions": ["Start metformin 500mg BID", "Diabetes education referral"],
                    "responsible": "PCP and CDE",
                    "target_date": "2026-10-18"
                }
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Care Plan", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Poorly controlled A1c", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("metformin", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Discharge_summary_template_renders_hospital_course()
    {
        var template = new DischargeSummaryTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "patient_name": "Jordan Sample",
            "admission_date": "2026-04-10",
            "discharge_date": "2026-04-15",
            "attending_physician": "Dr. Morgan Lee",
            "admission_diagnosis": "Community-acquired pneumonia.",
            "discharge_diagnosis": "Resolved community-acquired pneumonia.",
            "hospital_course": "Patient admitted with fever and productive cough. Treated with IV ceftriaxone. Improved over 5 days.",
            "discharge_instructions": "Complete oral antibiotic course. Follow up with PCP in 1 week.",
            "discharge_medications": ["Amoxicillin 500mg TID x7 days"],
            "follow_up_appointments": ["PCP — 2026-04-22"]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Discharge Summary", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Hospital Course", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Amoxicillin", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Hipaa_authorization_template_renders_required_clauses()
    {
        var template = new HipaaAuthorizationTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "patient_name": "Jordan Sample",
            "date_of_birth": "1988-03-14",
            "releasing_entity": "Lakeshore Family Medicine",
            "receiving_party": "Metro Cardiology Associates",
            "information_to_release": ["Office visit notes", "Lab results", "Imaging reports"],
            "purpose_of_release": "Cardiology consultation for chest pain workup.",
            "authorization_date": "2026-04-18",
            "expiration_date": "2027-04-18"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Authorization for Release", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Right to Revoke", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Redisclosure Notice", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Superbill_template_renders_codes_and_totals()
    {
        var template = new SuperbillTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "practice_name": "Lakeshore Family Medicine",
            "provider_name": "Dr. Morgan Lee",
            "patient_name": "Jordan Sample",
            "patient_dob": "1988-03-14",
            "date_of_service": "2026-04-18",
            "currency": "USD",
            "diagnoses": [
                {"code":"I10","description":"Essential hypertension"}
            ],
            "charges": [
                {"cpt_code":"99213","description":"Office visit, established, low complexity","units":1,"fee":125.00,"diagnosis_pointer":"1"},
                {"cpt_code":"36415","description":"Venipuncture","units":1,"fee":15.00}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Superbill", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("99213", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("I10", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("140.00", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Progress_note_template_renders_soap_sections()
    {
        var template = new ProgressNoteTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "patient_name": "Jordan Sample",
            "encounter_date": "2026-04-18",
            "provider_name": "Dr. Morgan Lee",
            "provider_role": "MD",
            "chief_complaint": "Follow-up hypertension.",
            "subjective": "Patient reports adherence to lisinopril. Denies dizziness.",
            "objective": "BP 128/82. Lungs clear. Heart regular rate and rhythm.",
            "vital_signs": ["BP 128/82", "HR 72"],
            "assessment": "Hypertension, well controlled on current regimen.",
            "plan": "Continue lisinopril. Recheck in 3 months."
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("S — Subjective", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("O — Objective", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("A — Assessment", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("P — Plan", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("BP 128/82", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Referral_letter_template_renders_clinical_summary()
    {
        var template = new ReferralLetterTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "letter_date": "2026-04-18",
            "referring_provider_name": "Dr. Morgan Lee",
            "referring_provider_role": "MD",
            "referring_practice": "Lakeshore Family Medicine",
            "receiving_provider_name": "Dr. Priya Singh",
            "receiving_provider_specialty": "Cardiology",
            "patient_name": "Jordan Sample",
            "patient_dob": "1988-03-14",
            "urgency": "Routine",
            "reason_for_referral": "Evaluation of exertional chest pain.",
            "clinical_summary": "38-year-old with two-week history of substernal chest pain on exertion. EKG sinus rhythm. Troponin negative.",
            "current_medications": ["Lisinopril 10mg daily"]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Dr. Priya Singh", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Cardiology", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Clinical Summary", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Routine", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Syllabus_template_renders_grading_and_schedule()
    {
        var template = new SyllabusTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "institution_name": "State University",
            "course_code": "CS 101",
            "course_title": "Introduction to Computer Science",
            "term": "Fall 2026",
            "credits": "3",
            "instructor_name": "Dr. Jamie Park",
            "instructor_email": "jpark@state.edu",
            "course_description": "A foundational survey of computing and programming concepts.",
            "learning_objectives": ["Write basic programs in Python", "Explain algorithmic complexity"],
            "grading_breakdown": [
                {"category":"Assignments","weight_percent":40,"description":"Weekly problem sets"},
                {"category":"Midterm","weight_percent":25},
                {"category":"Final","weight_percent":35}
            ],
            "schedule": [
                {"week":"1","topic":"Variables and expressions","readings":"Ch. 1"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("CS 101", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Learning Objectives", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Assignments", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Variables and expressions", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Lesson_plan_template_renders_procedure()
    {
        var template = new LessonPlanTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "teacher_name": "Ms. Rivera",
            "subject": "Math",
            "grade_level": "Grade 5",
            "lesson_title": "Multiplying Fractions",
            "lesson_date": "2026-04-22",
            "duration_minutes": 45,
            "objectives": ["Multiply two proper fractions", "Simplify the result"],
            "procedure": [
                {"phase":"Warm-up","minutes":5,"description":"Fraction review on the board."},
                {"phase":"Direct instruction","minutes":15,"description":"Model multiplication with visual fractions."},
                {"phase":"Guided practice","minutes":20,"description":"Students work in pairs on worksheet."}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Multiplying Fractions", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Warm-up", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Direct instruction (15 min)", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Report_card_template_renders_subjects_and_attendance()
    {
        var template = new ReportCardTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "school_name": "Lakeshore Elementary",
            "student_name": "Robin Learner",
            "grade_level": "Grade 4",
            "term": "Q2 2026",
            "report_date": "2026-04-18",
            "subjects": [
                {"subject":"Reading","teacher":"Ms. Rivera","grade":"A","effort":"E"},
                {"subject":"Math","teacher":"Mr. Chen","grade":"B+","effort":"S","comments":"Good progress on fractions."}
            ],
            "days_present": 42,
            "days_absent": 3,
            "promotion_status": "On track"
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Robin Learner", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Academic Performance", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Days present: 42", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Iep_template_renders_goals_and_team()
    {
        var template = new IepTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "district_name": "Lakeshore Public Schools",
            "school_name": "Lakeshore Middle School",
            "student_name": "Avery Student",
            "date_of_birth": "2013-08-15",
            "grade_level": "Grade 6",
            "primary_disability": "Specific Learning Disability",
            "iep_date": "2026-04-18",
            "next_review_date": "2027-04-18",
            "case_manager": "Ms. Patel",
            "present_levels_academic": "Reads at grade 4 level; strong in math computation.",
            "present_levels_functional": "Needs support with organization and task initiation.",
            "annual_goals": [
                {
                    "area":"Reading",
                    "goal":"Given a grade-level passage, Avery will answer comprehension questions with 80% accuracy.",
                    "measurement":"Curriculum-based measurement biweekly.",
                    "objectives":["Identify main idea with 80% accuracy","Summarize key details"]
                }
            ],
            "services": [
                {"service":"Resource room","provider":"Ms. Patel","frequency":"5x/week, 45 min","location":"Resource room"}
            ],
            "team_members": [
                {"name":"Ms. Patel","role":"Case Manager / Special Educator"},
                {"name":"Mr. Chen","role":"General Education Teacher"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Individualized Education Program", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Annual Goals", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Resource room", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("FERPA", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Transcript_template_renders_terms_and_gpa()
    {
        var template = new TranscriptTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "institution_name": "State University",
            "student_name": "Alex Graduate",
            "student_id": "SU-12345",
            "program": "B.S. Computer Science",
            "issue_date": "2026-04-18",
            "official": true,
            "terms": [
                {
                    "term":"Fall 2025",
                    "courses": [
                        {"course_code":"CS 101","course_title":"Intro to CS","credits":3,"grade":"A","grade_points":12},
                        {"course_code":"MATH 151","course_title":"Calculus I","credits":4,"grade":"B+","grade_points":13.2}
                    ],
                    "term_gpa":3.6
                }
            ],
            "cumulative_gpa": 3.6,
            "cumulative_credits": 7
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Official", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Fall 2025", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("CS 101", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Cumulative", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("FERPA", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Bid_proposal_template_renders_itemized_bid()
    {
        var template = new BidProposalTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "contractor_name": "Acme Construction",
            "client_name": "Beta Properties",
            "project_name": "Warehouse Expansion",
            "project_address": "200 Industrial Way",
            "bid_date": "2026-04-18",
            "bid_valid_until": "2026-05-18",
            "currency": "USD",
            "scope_summary": "Construction of 10,000 sqft warehouse expansion including foundation, slab, tilt-up panels, and roofing.",
            "line_items": [
                {"description":"Sitework","amount":45000},
                {"description":"Foundation and slab","amount":85000},
                {"description":"Tilt-up panels","amount":180000},
                {"description":"Roofing","amount":60000}
            ],
            "overhead_and_profit_percent": 10,
            "contingency_percent": 5
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.StructuredPdf, rendered.Format);
        Assert.Contains("Bid Proposal", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Warehouse Expansion", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Total bid price", rendered.Body, StringComparison.Ordinal);

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
    }

    [Fact]
    public async Task Change_order_template_renders_contract_math()
    {
        var template = new ChangeOrderTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "project_name": "Warehouse Expansion",
            "owner_name": "Beta Properties",
            "contractor_name": "Acme Construction",
            "change_order_number": "CO-003",
            "change_order_date": "2026-04-18",
            "reason": "Owner-requested addition of skylights.",
            "description_of_change": "Add 4 skylights to roof with required framing, flashing, and rough-in.",
            "currency": "USD",
            "original_contract_sum": 400000,
            "net_change_prior_orders": 15000,
            "this_change_amount": 12500,
            "days_added": 7
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Change Order", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("CO-003", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("427,500", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Daily_log_template_renders_crews_and_work()
    {
        var template = new DailyLogTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "project_name": "Warehouse Expansion",
            "log_date": "2026-04-18",
            "prepared_by": "Sam Foreman",
            "weather_am": "Partly cloudy",
            "weather_pm": "Sunny",
            "temperature_high_f": 72,
            "temperature_low_f": 55,
            "crews": [
                {"trade":"Framing","company":"Acme Framers","workers":6,"hours":8},
                {"trade":"Electrical","company":"Bolt Electric","workers":3,"hours":8}
            ],
            "equipment_on_site": ["Crawler crane", "Scissor lift"],
            "work_performed": "Erected framing on west elevation. Rough electrical in panel B."
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Daily Construction Log", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Crews on Site", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Framing", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Punch_list_template_renders_items()
    {
        var template = new PunchListTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "project_name": "Warehouse Expansion",
            "walkthrough_date": "2026-04-18",
            "prepared_by": "Sam Foreman",
            "owner_representative": "Dana Owner",
            "contractor": "Acme Construction",
            "items": [
                {"location":"Office 101","trade":"Paint","description":"Touch up wall scuff near door","status":"Open"},
                {"location":"Main floor","trade":"Electrical","description":"Replace burned-out LED lamp at bay 4","assigned_to":"Bolt Electric","due_date":"2026-04-25","status":"In progress"}
            ]
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Punch List", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Office 101", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Bolt Electric", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task Safety_report_template_renders_narrative_and_actions()
    {
        var template = new SafetyReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "project_name": "Warehouse Expansion",
            "report_date": "2026-04-18",
            "incident_date": "2026-04-18",
            "incident_time": "10:15",
            "incident_type": "Near miss",
            "severity": "First aid",
            "reported_by": "Sam Foreman",
            "reporter_role": "Site superintendent",
            "location_on_site": "Bay 4 mezzanine",
            "narrative": "Unsecured plank slipped on mezzanine; worker caught balance on rail. No injury but potential for fall from 12 ft.",
            "immediate_causes": ["Unsecured plank", "Rushed setup"],
            "root_causes": ["Pre-task plan not reviewed with crew"],
            "corrective_actions": [
                {"action":"Re-train crew on fall protection and plank securement","responsible":"Sam Foreman","due_date":"2026-04-20"}
            ],
            "osha_recordable": false
        }
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Equal(DocumentFormat.Word, rendered.Format);
        Assert.Contains("Safety Incident Report", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Near miss", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Corrective Actions", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("OSHA", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
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
        new ServiceAgreementTemplate(),
        new EngagementLetterTemplate(),
        new DemandLetterTemplate(),
        new CorporateMinutesTemplate(),
        new PowerOfAttorneyTemplate(),
        new TermsOfServiceTemplate(),
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
