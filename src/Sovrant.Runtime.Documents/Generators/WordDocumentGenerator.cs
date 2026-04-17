using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Renders the Markdown body into a .docx using the OpenXml SDK.
/// Heading blocks map to Word's built-in Heading 1–6 styles; paragraphs
/// carry inline bold/italic/code; lists become bulleted or numbered
/// runs using the document's default Numbering part.
/// </summary>
public sealed class WordDocumentGenerator : DocumentGeneratorBase
{
    private const string CodeFontFamily = "Consolas";

    public WordDocumentGenerator(IArtifactStore artifactStore) : base(artifactStore) { }

    public override DocumentFormat Format => DocumentFormat.Word;

    protected override string ContentType =>
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, autoSave: true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            EnsureStyles(mainPart);
            EnsureNumbering(mainPart);

            if (!string.IsNullOrWhiteSpace(request.Title))
                body.AppendChild(BuildHeadingParagraph(request.Title, level: 1));

            foreach (var block in MarkdownModel.Parse(request.Body))
            {
                ct.ThrowIfCancellationRequested();
                AppendBlock(body, block);
            }
        }

        return Task.FromResult(ms.ToArray());
    }

    private static void AppendBlock(Body body, MarkdownBlock block)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                var heading = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{block.Level}" }));
                AppendRuns(heading, block.Inlines);
                body.AppendChild(heading);
                break;

            case MarkdownBlockKind.Paragraph:
                var para = new Paragraph();
                AppendRuns(para, block.Inlines);
                body.AppendChild(para);
                break;

            case MarkdownBlockKind.CodeBlock:
                foreach (var line in (block.RawText ?? string.Empty).Split('\n'))
                {
                    var codePara = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Code" }));
                    var run = new Run(
                        new RunProperties(new RunFonts { Ascii = CodeFontFamily, HighAnsi = CodeFontFamily }),
                        new Text(line) { Space = SpaceProcessingModeValues.Preserve });
                    codePara.AppendChild(run);
                    body.AppendChild(codePara);
                }
                break;

            case MarkdownBlockKind.List:
                if (block.ListItems is null) break;
                var numId = block.Ordered ? 2 : 1;
                foreach (var itemRuns in block.ListItems)
                {
                    var listPara = new Paragraph(
                        new ParagraphProperties(
                            new ParagraphStyleId { Val = "ListParagraph" },
                            new NumberingProperties(
                                new NumberingLevelReference { Val = 0 },
                                new NumberingId { Val = numId })));
                    AppendRuns(listPara, itemRuns);
                    body.AppendChild(listPara);
                }
                break;

            case MarkdownBlockKind.Quote:
                var quote = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "IntenseQuote" }));
                AppendRuns(quote, block.Inlines);
                body.AppendChild(quote);
                break;

            case MarkdownBlockKind.ThematicBreak:
                body.AppendChild(new Paragraph(new Run(new Text(new string('—', 40)))));
                break;
        }
    }

    private static Paragraph BuildHeadingParagraph(string text, int level)
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{level}" }));
        paragraph.AppendChild(new Run(new Text(text)));
        return paragraph;
    }

    private static void AppendRuns(Paragraph paragraph, IReadOnlyList<MarkdownRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.Text == "\n")
            {
                paragraph.AppendChild(new Run(new Break()));
                continue;
            }

            var runProps = new RunProperties();
            if (run.Bold) runProps.AppendChild(new Bold());
            if (run.Italic) runProps.AppendChild(new Italic());
            if (run.Code)
                runProps.AppendChild(new RunFonts { Ascii = CodeFontFamily, HighAnsi = CodeFontFamily });

            var runElement = new Run();
            if (runProps.HasChildren)
                runElement.AppendChild(runProps);
            runElement.AppendChild(new Text(run.Text) { Space = SpaceProcessingModeValues.Preserve });
            paragraph.AppendChild(runElement);
        }
    }

    private static void EnsureStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();
        styles.AppendChild(BuildHeadingStyle(1, 32));
        styles.AppendChild(BuildHeadingStyle(2, 28));
        styles.AppendChild(BuildHeadingStyle(3, 24));
        styles.AppendChild(BuildHeadingStyle(4, 22));
        styles.AppendChild(BuildHeadingStyle(5, 20));
        styles.AppendChild(BuildHeadingStyle(6, 20));
        styles.AppendChild(new Style(
            new StyleName { Val = "Code" },
            new BasedOn { Val = "Normal" },
            new StyleRunProperties(new RunFonts { Ascii = CodeFontFamily, HighAnsi = CodeFontFamily }, new FontSize { Val = "18" }))
        {
            Type = StyleValues.Paragraph,
            StyleId = "Code",
        });
        styles.AppendChild(new Style(
            new StyleName { Val = "List Paragraph" },
            new BasedOn { Val = "Normal" })
        {
            Type = StyleValues.Paragraph,
            StyleId = "ListParagraph",
        });
        stylesPart.Styles = styles;
        stylesPart.Styles.Save();
    }

    private static Style BuildHeadingStyle(int level, int halfPointSize) => new(
        new StyleName { Val = $"Heading {level}" },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new StyleRunProperties(new Bold(), new FontSize { Val = halfPointSize.ToString(System.Globalization.CultureInfo.InvariantCulture) }))
    {
        Type = StyleValues.Paragraph,
        StyleId = $"Heading{level}",
    };

    private static void EnsureNumbering(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        var numbering = new Numbering();

        numbering.AppendChild(new AbstractNum(
            new Level(
                new NumberingFormat { Val = NumberFormatValues.Bullet },
                new LevelText { Val = "•" },
                new RunProperties(new RunFonts { Ascii = "Symbol", HighAnsi = "Symbol" }))
            { LevelIndex = 0 })
        { AbstractNumberId = 1 });

        numbering.AppendChild(new AbstractNum(
            new Level(
                new NumberingFormat { Val = NumberFormatValues.Decimal },
                new LevelText { Val = "%1." })
            { LevelIndex = 0 })
        { AbstractNumberId = 2 });

        numbering.AppendChild(new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = 1 });
        numbering.AppendChild(new NumberingInstance(new AbstractNumId { Val = 2 }) { NumberID = 2 });

        numberingPart.Numbering = numbering;
        numberingPart.Numbering.Save();
    }
}
