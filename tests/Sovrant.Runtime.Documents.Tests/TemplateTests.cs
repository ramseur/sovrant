using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;
using Sovrant.Runtime.Documents.Generators;
using Sovrant.Runtime.Documents.Templates;
using Sovrant.Runtime.Documents.Templates.Finance;
using Sovrant.Runtime.Knowledge;

namespace Sovrant.Runtime.Documents.Tests;

/// <summary>
/// Template rendering tests for Phase 112D. The 42 built-in document templates
/// are now DB-backed (<see cref="DbDocumentTemplate"/>); only the two Excel templates
/// remain as C# classes (<see cref="ExpenseReportTemplate"/>, <see cref="LoanAmortizationTemplate"/>).
/// </summary>
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

    // ── TemplateData validation ───────────────────────────────────────────────

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

    // ── TemplateFieldSerializer roundtrip ─────────────────────────────────────

    [Fact]
    public void TemplateFieldSerializer_roundtrips_nested_fields()
    {
        var fields = new List<TemplateField>
        {
            new() { Name = "title", Type = TemplateFieldType.String, Required = true, Description = "Meeting title." },
            new()
            {
                Name = "action_items",
                Type = TemplateFieldType.ObjectArray,
                Required = false,
                ItemFields = new List<TemplateField>
                {
                    new() { Name = "task", Type = TemplateFieldType.String, Required = true },
                    new() { Name = "due_date", Type = TemplateFieldType.Date, Required = false },
                },
            },
        };

        var json = TemplateFieldSerializer.Serialize(fields);
        var roundtripped = TemplateFieldSerializer.Deserialize(json);

        Assert.Equal(2, roundtripped.Count);
        Assert.Equal("title", roundtripped[0].Name);
        Assert.Equal(TemplateFieldType.String, roundtripped[0].Type);
        Assert.True(roundtripped[0].Required);
        Assert.Equal("action_items", roundtripped[1].Name);
        Assert.Equal(TemplateFieldType.ObjectArray, roundtripped[1].Type);
        Assert.NotNull(roundtripped[1].ItemFields);
        Assert.Equal(2, roundtripped[1].ItemFields!.Count);
        Assert.Equal("task", roundtripped[1].ItemFields[0].Name);
    }

    // ── DbDocumentTemplate rendering ──────────────────────────────────────────

    [Fact]
    public async Task DbDocumentTemplate_meeting_notes_renders_to_word()
    {
        var template = MakeMeetingNotesTemplate();
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
        Assert.Contains("Q2 Planning", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Alice", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Ship feature X in May", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.ContentType);
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public async Task DbDocumentTemplate_invoice_renders_to_structured_pdf()
    {
        var template = MakeInvoiceTemplate();
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

        var generator = new MigraDocGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.Equal("application/pdf", result.ContentType);
        Assert.True(result.SizeBytes > 500);
    }

    [Fact]
    public async Task DbDocumentTemplate_nda_renders_to_word()
    {
        var template = MakeNdaTemplate();
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
        Assert.Contains("Beta LLC", rendered.Body, StringComparison.Ordinal);
        Assert.Contains("Delaware", rendered.Body, StringComparison.Ordinal);

        var generator = new WordDocumentGenerator(_store);
        var result = await generator.GenerateAsync(BuildRequest(rendered));
        Assert.True(await IsZipArchiveAsync(result));
    }

    [Fact]
    public void DbDocumentTemplate_filename_uses_template_expression()
    {
        var template = MakeMeetingNotesTemplate();
        using var doc = JsonDocument.Parse("""
        {"title":"Sprint Planning","meeting_date":"2026-05-01","attendees":["Alice"]}
        """);

        var rendered = template.Render(doc.RootElement);
        Assert.Contains("sprint-planning", rendered.DefaultFileName, StringComparison.Ordinal);
        Assert.EndsWith(".docx", rendered.DefaultFileName, StringComparison.Ordinal);
    }

    [Fact]
    public void DbDocumentTemplate_resolves_default_format_from_string()
    {
        var page = MakePage("test/word", "Test", "Word", "{{ title }}", "[]",
            "{{ title }}.docx");
        var template = new DbDocumentTemplate(page);
        Assert.Equal(DocumentFormat.Word, template.DefaultFormat);

        var page2 = MakePage("test/pdf", "Test2", "StructuredPdf", "# {{ title }}", "[]",
            "{{ title }}.pdf");
        var template2 = new DbDocumentTemplate(page2);
        Assert.Equal(DocumentFormat.StructuredPdf, template2.DefaultFormat);
    }

    [Fact]
    public void DbDocumentTemplate_empty_fields_json_gives_no_fields()
    {
        var page = MakePage("test/empty", "Empty", "Word", "hello", null, "empty.docx");
        var template = new DbDocumentTemplate(page);
        Assert.Empty(template.Fields);
    }

    // ── TemplateRegistry ──────────────────────────────────────────────────────

    [Fact]
    public void Registry_filters_by_industry_and_search()
    {
        var fakeStore = new FakeTemplateStore();
        fakeStore.Add(MakePage("legal/nda", "Non-Disclosure Agreement", "Word",
            "# NDA", MakeFieldsJson([("party_a", "string", true)]), "nda.docx",
            industry: "legal"));
        fakeStore.Add(MakePage("finance/invoice", "Invoice", "StructuredPdf",
            "# Invoice", MakeFieldsJson([("invoice_number", "string", true)]), "invoice.pdf",
            industry: "finance"));

        var registry = new TemplateRegistry(
            Array.Empty<IDocumentTemplate>(), // no C# templates
            fakeStore,
            NullLogger<TemplateRegistry>.Instance);

        var legal = registry.Find(industry: "legal").ToList();
        Assert.Single(legal);
        Assert.Equal("legal/nda", legal[0].Id);

        var hits = registry.Find(search: "invoice").ToList();
        Assert.Single(hits);
        Assert.Equal("finance/invoice", hits[0].Id);
    }

    [Fact]
    public void Registry_db_template_shadows_csharp_template()
    {
        // C# LoanAmortization has Id "finance/loan-amortization"
        // If a DB row with same Id is present, the DB version wins.
        var fakeStore = new FakeTemplateStore();
        fakeStore.Add(MakePage("finance/loan-amortization", "Loan (DB override)", "Word",
            "# DB override", "[]", "loan.docx", industry: "finance"));

        var registry = new TemplateRegistry(
            new IDocumentTemplate[] { new LoanAmortizationTemplate() },
            fakeStore,
            NullLogger<TemplateRegistry>.Instance);

        var t = registry.Resolve("finance/loan-amortization");
        Assert.IsType<DbDocumentTemplate>(t);
        Assert.Equal("Loan (DB override)", t.Name);
    }

    // ── C# Excel templates (remain as C#) ─────────────────────────────────────

    [Fact]
    public async Task Expense_report_template_renders_to_excel()
    {
        var template = new ExpenseReportTemplate();
        using var doc = JsonDocument.Parse("""
        {
            "employee_name": "Jane Doe",
            "period_start": "2026-04-01",
            "period_end": "2026-04-15",
            "expenses": [
                {"date":"2026-04-02","category":"Flight","description":"SFO to JFK","amount":450},
                {"date":"2026-04-03","category":"Hotel","description":"2 nights","amount":300}
            ]
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
        Assert.True(await IsZipArchiveAsync(result));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static KnowledgePage MakePage(
        string slug, string name, string format, string body,
        string? fieldsJson, string? filenameTpl, string industry = "general")
    {
        var now = DateTimeOffset.UtcNow;
        return new KnowledgePage(
            KnowledgeId:      $"doctempl_{slug.Replace('/', '-')}",
            Kind:             "document-templates",
            Slug:             slug,
            Name:             name,
            Description:      string.Empty,
            Tier:             "BuiltIn",
            Body:             body,
            WorkspaceId:      string.Empty,
            CreatedAt:        now,
            UpdatedAt:        now,
            Industry:         industry,
            DefaultFormat:    format,
            FieldsJson:       fieldsJson,
            FilenameTemplate: filenameTpl);
    }

    private static string MakeFieldsJson(IEnumerable<(string name, string type, bool required)> fields)
    {
        var parts = fields.Select(f =>
            $"{{\"name\":\"{f.name}\",\"type\":\"{f.type}\",\"required\":{(f.required ? "true" : "false")}}}");
        return $"[{string.Join(",", parts)}]";
    }

    // Pre-built DB templates that mirror the V037 seed body for representative tests

    private static DbDocumentTemplate MakeMeetingNotesTemplate()
    {
        const string body = """
            # {{ title }}

            **Date:** {{ format_date meeting_date }}{{ if location && location != "" }}
            **Location:** {{ location }}{{ end }}

            {{ if attendees && attendees.size > 0 }}
            ## Attendees
            {{ for a in attendees }}- {{ a }}
            {{ end }}
            {{ end }}
            {{ if agenda && agenda.size > 0 }}
            ## Agenda
            {{ i = 1 }}{{ for item in agenda }}{{ i }}. {{ item }}
            {{ i = i + 1 }}{{ end }}
            {{ end }}
            {{ if discussion && discussion != "" }}
            ## Discussion
            {{ discussion }}

            {{ end }}
            {{ if decisions && decisions.size > 0 }}
            ## Decisions
            {{ for d in decisions }}- {{ d }}
            {{ end }}
            {{ end }}
            {{ if action_items && action_items.size > 0 }}
            ## Action Items
            {{ for item in action_items }}- {{ item.task }}{{ if item.owner && item.owner != "" }} — **{{ item.owner }}**{{ end }}{{ if item.due_date && item.due_date != "" }} (due {{ format_date item.due_date }}){{ end }}
            {{ end }}
            {{ end }}
            """;

        var fields = """
            [{"name":"title","type":"string","required":true},
             {"name":"meeting_date","type":"date","required":true},
             {"name":"location","type":"string","required":false},
             {"name":"attendees","type":"stringArray","required":true},
             {"name":"agenda","type":"stringArray","required":false},
             {"name":"discussion","type":"text","required":false},
             {"name":"decisions","type":"stringArray","required":false},
             {"name":"action_items","type":"objectArray","required":false}]
            """;

        return new DbDocumentTemplate(MakePage("business/meeting-notes", "Meeting Notes", "Word",
            body, fields, "meeting-notes-{{ format_date meeting_date }}-{{ slug title }}.docx",
            "business"));
    }

    private static DbDocumentTemplate MakeInvoiceTemplate()
    {
        const string body = """
            {{- currency_val = normalize_currency currency -}}
            # Invoice {{ invoice_number }}

            **Issue date:** {{ format_date issue_date }}{{ if due_date && due_date != "" }}
            **Due date:** {{ format_date due_date }}{{ end }}

            ## From
            {{ seller }}

            ## Bill to
            {{ buyer }}

            ## Line items

            | Description | Qty | Unit price | Subtotal |
            |---|---:|---:|---:|
            {{- gross_total = 0 }}
            {{ for item in line_items -}}
            {{- line_total = item.quantity * item.unit_price -}}
            {{- gross_total = gross_total + line_total -}}
            | {{ escape_pipes item.description }} | {{ format_number item.quantity }} | {{ format_money item.unit_price currency_val }} | {{ format_money line_total currency_val }} |
            {{ end }}

            ## Totals

            | | |
            |---|---:|
            | Subtotal | {{ format_money gross_total currency_val }} |
            {{- if tax_rate > 0 }}
            {{- tax_amount = math.round(gross_total * (tax_rate / 100), 2) }}
            | Tax ({{ format_percent tax_rate }}) | {{ format_money tax_amount currency_val }} |
            {{- end }}
            | **Total** | **{{ format_money (gross_total + (tax_rate > 0 ? math.round(gross_total * (tax_rate / 100), 2) : 0)) currency_val }}** |

            {{ if payment_terms && payment_terms != "" }}
            ## Payment terms
            {{ payment_terms }}
            {{ end }}
            """;

        var fields = """
            [{"name":"invoice_number","type":"string","required":true},
             {"name":"issue_date","type":"date","required":true},
             {"name":"due_date","type":"date","required":false},
             {"name":"currency","type":"string","required":false},
             {"name":"seller","type":"text","required":true},
             {"name":"buyer","type":"text","required":true},
             {"name":"line_items","type":"objectArray","required":true,"itemFields":[
               {"name":"description","type":"string","required":true},
               {"name":"quantity","type":"decimal","required":true},
               {"name":"unit_price","type":"decimal","required":true}]},
             {"name":"tax_rate","type":"decimal","required":false},
             {"name":"payment_terms","type":"text","required":false}]
            """;

        return new DbDocumentTemplate(MakePage("finance/invoice", "Invoice", "StructuredPdf",
            body, fields, "invoice-{{ slug invoice_number }}.pdf", "finance"));
    }

    private static DbDocumentTemplate MakeNdaTemplate()
    {
        const string body = """
            # Mutual Non-Disclosure Agreement

            This Mutual Non-Disclosure Agreement (the "Agreement") is entered into as of {{ format_date effective_date }} by and between **{{ party_a }}** ("Party A") and **{{ party_b }}** ("Party B"), collectively the "Parties."

            ## 1. Purpose

            {{ purpose }}

            ## 2. Confidential Information

            "Confidential Information" means any non-public information disclosed by one Party to the other that is marked or identified as confidential.

            ## 3. Term

            This Agreement continues for {{ term_years }} {{ if term_years == 1 }}year{{ else }}years{{ end }}, unless terminated earlier by mutual written agreement.

            ## 8. Governing Law

            This Agreement is governed by the laws of **{{ governing_law }}**.

            ## Signatures

            **{{ party_a }}**

            Signature: ____________________________

            **{{ party_b }}**

            Signature: ____________________________
            """;

        var fields = """
            [{"name":"party_a","type":"string","required":true},
             {"name":"party_b","type":"string","required":true},
             {"name":"effective_date","type":"date","required":true},
             {"name":"term_years","type":"integer","required":false},
             {"name":"purpose","type":"text","required":true},
             {"name":"governing_law","type":"string","required":true}]
            """;

        return new DbDocumentTemplate(MakePage("legal/nda", "Non-Disclosure Agreement", "Word",
            body, fields, "nda-{{ slug party_a }}-{{ slug party_b }}-{{ format_date effective_date }}.docx",
            "legal"));
    }

    private static DocumentRequest BuildRequest(TemplateRenderResult rendered) => new()
    {
        Format = rendered.Format,
        Body = rendered.Body,
        FileName = rendered.DefaultFileName,
        Title = rendered.Title,
        Scope = new ArtifactScope
        {
            WorkspaceId = "ws-test",
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
        catch (InvalidDataException) { return false; }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { if (Directory.Exists(_artifactRoot)) Directory.Delete(_artifactRoot, recursive: true); }
        catch (IOException) { /* best effort */ }
    }
}

/// <summary>Fake <see cref="IKnowledgeStore"/> for <see cref="TemplateRegistry"/> tests.</summary>
internal sealed class FakeTemplateStore : IKnowledgeStore
{
    private readonly List<KnowledgePage> _pages = [];

    public void Add(KnowledgePage page) => _pages.Add(page);

    public Task<IReadOnlyList<KnowledgePage>> GetAllAsync(string kind, string workspaceId = "", CancellationToken ct = default)
    {
        IReadOnlyList<KnowledgePage> r = _pages.Where(p => p.Kind == kind && p.WorkspaceId == workspaceId).ToList();
        return Task.FromResult(r);
    }

    public Task<KnowledgePage?> GetAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.Kind == kind && p.Slug == slug && p.WorkspaceId == workspaceId));

    public Task<KnowledgePage?> GetActiveAsync(string kind, string slug, CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.Kind == kind && p.Slug == slug));

    public Task<KnowledgePage?> GetEffectiveAsync(string kind, string slug, string projectId = "", CancellationToken ct = default) =>
        Task.FromResult(_pages.FirstOrDefault(p => p.Kind == kind && p.Slug == slug));

    public Task<IReadOnlyList<KnowledgePage>> GetAllEffectiveAsync(string kind, string projectId = "", CancellationToken ct = default)
    {
        IReadOnlyList<KnowledgePage> r = _pages.Where(p => p.Kind == kind).ToList();
        return Task.FromResult(r);
    }

    public Task UpsertAsync(KnowledgePage page, CancellationToken ct = default)
    {
        _pages.Add(page); return Task.CompletedTask;
    }

    public Task DeleteAsync(string kind, string slug, string workspaceId = "", CancellationToken ct = default)
    {
        _pages.RemoveAll(p => p.Kind == kind && p.Slug == slug && p.WorkspaceId == workspaceId);
        return Task.CompletedTask;
    }
}
