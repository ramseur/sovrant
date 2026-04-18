using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Structured-PDF generator. Parses the request body as Markdown (via
/// Markdig) and walks the AST into a MigraDoc document — headings, lists,
/// paragraphs, inline emphasis, code blocks and fenced code. Tables and
/// images are out of scope for the initial pass; they hit the paragraph
/// fallback so nothing is dropped silently.
/// </summary>
public sealed class MigraDocGenerator : DocumentGeneratorBase
{
    private const string BodyFontFamily = "Arial";
    private const string CodeFontFamily = "Arial";

    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly object s_fontResolverLock = new();
    private static bool s_fontResolverInitialised;

    public MigraDocGenerator(IArtifactStore artifactStore) : base(artifactStore)
    {
        EnsureFontResolver();
    }

    public override DocumentFormat Format => DocumentFormat.StructuredPdf;

    protected override string ContentType => "application/pdf";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        var document = BuildDocument(request, ct);

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var ms = new MemoryStream();
        renderer.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    private static Document BuildDocument(DocumentRequest request, CancellationToken ct)
    {
        var document = new Document();
        document.Info.Title = request.Title ?? Path.GetFileNameWithoutExtension(request.FileName);
        document.Info.Author = "Sovrant";

        DefineStyles(document);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var titlePara = section.AddParagraph(request.Title);
            titlePara.Style = "DocTitle";
        }

        var ast = Markdown.Parse(request.Body ?? string.Empty, s_pipeline);
        foreach (var block in ast)
        {
            ct.ThrowIfCancellationRequested();
            RenderBlock(section, block);
        }

        return document;
    }

    private static void DefineStyles(Document document)
    {
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = BodyFontFamily;
        normal.Font.Size = 10;

        var title = document.Styles.AddStyle("DocTitle", "Normal");
        title.Font.Size = 18;
        title.Font.Bold = true;
        title.ParagraphFormat.SpaceAfter = Unit.FromPoint(12);

        for (var level = 1; level <= 6; level++)
        {
            var heading = document.Styles.AddStyle($"H{level}", "Normal");
            heading.Font.Bold = true;
            heading.Font.Size = level switch { 1 => 16, 2 => 14, 3 => 12, 4 => 11, _ => 10 };
            heading.ParagraphFormat.SpaceBefore = Unit.FromPoint(10);
            heading.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        }

        var code = document.Styles.AddStyle("Code", "Normal");
        code.Font.Name = CodeFontFamily;
        code.Font.Size = 9;
        code.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.5);
        code.ParagraphFormat.SpaceBefore = Unit.FromPoint(4);
        code.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);

        var listItem = document.Styles.AddStyle("ListItem", "Normal");
        listItem.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.8);
        listItem.ParagraphFormat.FirstLineIndent = Unit.FromCentimeter(-0.4);
    }

    private static void RenderBlock(Section section, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var hp = section.AddParagraph();
                hp.Style = $"H{Math.Clamp(heading.Level, 1, 6)}";
                AppendInlines(hp, heading.Inline);
                break;

            case ParagraphBlock paragraph:
                var pp = section.AddParagraph();
                AppendInlines(pp, paragraph.Inline);
                break;

            case FencedCodeBlock fenced:
                var codePara = section.AddParagraph(string.Join('\n', fenced.Lines.Lines.Select(l => l.ToString())));
                codePara.Style = "Code";
                break;

            case CodeBlock code:
                var indentPara = section.AddParagraph(string.Join('\n', code.Lines.Lines.Select(l => l.ToString())));
                indentPara.Style = "Code";
                break;

            case ListBlock list:
                foreach (var item in list)
                {
                    if (item is ListItemBlock li)
                    {
                        var itemPara = section.AddParagraph();
                        itemPara.Style = "ListItem";
                        itemPara.AddText(list.IsOrdered ? "• " : "• ");
                        foreach (var subBlock in li)
                        {
                            if (subBlock is ParagraphBlock p && p.Inline is not null)
                                AppendInlines(itemPara, p.Inline);
                        }
                    }
                }
                break;

            case QuoteBlock quote:
                foreach (var inner in quote)
                    RenderBlock(section, inner);
                break;

            case ThematicBreakBlock:
                section.AddParagraph(new string('─', 40));
                break;

            case Markdig.Extensions.Tables.Table table:
                RenderTable(section, table);
                break;

            default:
                // Fallback — dump the raw markdown so nothing goes missing.
                var fallback = section.AddParagraph(block.ToString() ?? string.Empty);
                fallback.Style = "Normal";
                break;
        }
    }

    private static void RenderTable(Section section, Markdig.Extensions.Tables.Table source)
    {
        var columnCount = source.ColumnDefinitions.Count > 0
            ? source.ColumnDefinitions.Count
            : source.OfType<Markdig.Extensions.Tables.TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
        if (columnCount == 0) return;

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Colors.Gray;
        table.LeftPadding = Unit.FromPoint(4);
        table.RightPadding = Unit.FromPoint(4);
        table.TopPadding = Unit.FromPoint(2);
        table.BottomPadding = Unit.FromPoint(2);

        for (var i = 0; i < columnCount; i++)
            table.AddColumn(Unit.FromCentimeter(17.0 / columnCount));

        foreach (var rowObj in source)
        {
            if (rowObj is not Markdig.Extensions.Tables.TableRow mdRow) continue;

            var row = table.AddRow();
            if (mdRow.IsHeader)
            {
                row.Shading.Color = Colors.LightGray;
                row.Format.Font.Bold = true;
            }

            for (var c = 0; c < columnCount; c++)
            {
                var cellPara = row.Cells[c].AddParagraph();
                if (c < mdRow.Count && mdRow[c] is Markdig.Extensions.Tables.TableCell mdCell)
                {
                    foreach (var block in mdCell)
                    {
                        if (block is ParagraphBlock p) AppendInlines(cellPara, p.Inline);
                    }
                }
            }
        }
    }

    private static void AppendInlines(Paragraph target, ContainerInline? inlines)
    {
        if (inlines is null) return;

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.AddText(literal.Content.ToString());
                    break;

                case EmphasisInline emphasis:
                    var text = ExtractText(emphasis);
                    var run = target.AddFormattedText(text);
                    if (emphasis.DelimiterCount >= 2) run.Bold = true;
                    else run.Italic = true;
                    break;

                case CodeInline code:
                    var codeRun = target.AddFormattedText(code.Content);
                    codeRun.Font.Name = CodeFontFamily;
                    codeRun.Font.Size = 9;
                    break;

                case LineBreakInline:
                    target.AddLineBreak();
                    break;

                case LinkInline link:
                    var linkText = ExtractText(link);
                    if (!string.IsNullOrEmpty(link.Url))
                    {
                        var hyperlink = target.AddHyperlink(link.Url, HyperlinkType.Url);
                        hyperlink.AddFormattedText(string.IsNullOrEmpty(linkText) ? link.Url : linkText);
                    }
                    else
                    {
                        target.AddText(linkText);
                    }
                    break;

                default:
                    target.AddText(ExtractText(inline));
                    break;
            }
        }
    }

    private static string ExtractText(Inline inline)
    {
        return inline switch
        {
            LiteralInline lit => lit.Content.ToString(),
            CodeInline code => code.Content,
            ContainerInline container => string.Concat(container.Select(ExtractText)),
            _ => string.Empty,
        };
    }

    private static void EnsureFontResolver()
    {
        if (s_fontResolverInitialised) return;
        lock (s_fontResolverLock)
        {
            if (s_fontResolverInitialised) return;
            if (GlobalFontSettings.FontResolver is null)
            {
                GlobalFontSettings.FontResolver = new DocumentFontResolver();
            }
            s_fontResolverInitialised = true;
        }
    }
}
