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
