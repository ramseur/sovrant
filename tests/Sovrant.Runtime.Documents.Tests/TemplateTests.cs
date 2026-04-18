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
        Assert.Contains(invoiceHits, t => t.Id == "business/invoice");
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

    private static IEnumerable<IDocumentTemplate> AllTemplates() => new IDocumentTemplate[]
    {
        new InvoiceTemplate(),
        new MeetingNotesTemplate(),
        new NdaTemplate(),
        new ExpenseReportTemplate(),
        new PropertyListingTemplate(),
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
