using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Sovrant.Runtime.Artifacts;
using A = DocumentFormat.OpenXml.Drawing;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Renders Markdown into a .pptx. Every H1 starts a new slide (the
/// heading text becomes the slide title); subsequent blocks — paragraphs,
/// list items, code blocks — turn into bullet points on that slide.
/// </summary>
/// <remarks>
/// OpenXml's presentation package is verbose: every .pptx needs a slide
/// master, slide layout, theme and presentation part. We build a minimal
/// set from scratch rather than copy a template, so the output opens in
/// PowerPoint, Google Slides, and LibreOffice Impress without errors.
/// </remarks>
public sealed class PowerPointGenerator : DocumentGeneratorBase
{
    public PowerPointGenerator(IArtifactStore artifactStore) : base(artifactStore) { }

    public override DocumentFormat Format => DocumentFormat.PowerPoint;

    protected override string ContentType =>
        "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        var slides = SlicedSlides(request);

        using var ms = new MemoryStream();
        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, autoSave: true))
        {
            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new P.Presentation();

            var slideMasterPart = CreateSlideMasterPart(presentationPart);
            var slideLayoutPart = CreateSlideLayoutPart(slideMasterPart);

            var slideIdList = new SlideIdList();
            uint slideId = 256;

            foreach (var slide in slides)
            {
                ct.ThrowIfCancellationRequested();
                var slidePart = CreateSlidePart(presentationPart, slideLayoutPart, slide);
                var relId = presentationPart.GetIdOfPart(slidePart);
                slideIdList.AppendChild(new SlideId { Id = slideId++, RelationshipId = relId });
            }

            presentationPart.Presentation.AppendChild(slideIdList);
            presentationPart.Presentation.AppendChild(new SlideSize { Cx = 9144000, Cy = 6858000 });
            presentationPart.Presentation.AppendChild(new NotesSize { Cx = 6858000, Cy = 9144000 });
            presentationPart.Presentation.AppendChild(new DefaultTextStyle());
            presentationPart.Presentation.AppendChild(
                new SlideMasterIdList(new SlideMasterId
                {
                    Id = 2147483648U,
                    RelationshipId = presentationPart.GetIdOfPart(slideMasterPart),
                }));
            MoveMasterIdListToTop(presentationPart.Presentation);

            presentationPart.Presentation.Save();
        }

        return Task.FromResult(ms.ToArray());
    }

    private static void MoveMasterIdListToTop(P.Presentation presentation)
    {
        // OpenXml requires slideMasterIdLst before sldIdLst in the schema.
        var masterList = presentation.GetFirstChild<SlideMasterIdList>();
        if (masterList is null) return;
        masterList.Remove();
        presentation.InsertAt(masterList, 0);
    }

    private static List<SlideContent> SlicedSlides(DocumentRequest request)
    {
        var slides = new List<SlideContent>();
        var blocks = MarkdownModel.Parse(request.Body);

        SlideContent? current = null;

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            current = new SlideContent { Title = request.Title };
            slides.Add(current);
        }

        foreach (var block in blocks)
        {
            if (block.Kind == MarkdownBlockKind.Heading && block.Level <= 2)
            {
                current = new SlideContent { Title = InlinesToString(block.Inlines) };
                slides.Add(current);
                continue;
            }

            current ??= new SlideContent { Title = request.Title ?? "Slide" };
            if (slides.Count == 0) slides.Add(current);

            switch (block.Kind)
            {
                case MarkdownBlockKind.Paragraph:
                    current.Bullets.Add(InlinesToString(block.Inlines));
                    break;
                case MarkdownBlockKind.List:
                    if (block.ListItems is null) break;
                    foreach (var item in block.ListItems)
                        current.Bullets.Add(InlinesToString(item));
                    break;
                case MarkdownBlockKind.CodeBlock:
                    current.Bullets.Add(block.RawText ?? string.Empty);
                    break;
                case MarkdownBlockKind.Heading:
                    current.Bullets.Add(InlinesToString(block.Inlines));
                    break;
            }
        }

        if (slides.Count == 0)
            slides.Add(new SlideContent { Title = request.Title ?? "Untitled" });

        return slides;
    }

    private static string InlinesToString(IReadOnlyList<MarkdownRun> runs) =>
        string.Concat(runs.Select(r => r.Text));

    // Standard 4:3 slide is 9144000 × 6858000 EMU. Anchor the title in the
    // upper band and give the body the remainder so Google Slides renders
    // content instead of rejecting the file for missing geometry.
    private const long TitleOffsetX = 685800L;
    private const long TitleOffsetY = 457200L;
    private const long TitleWidth = 7772400L;
    private const long TitleHeight = 1143000L;
    private const long BodyOffsetX = 685800L;
    private const long BodyOffsetY = 1600200L;
    private const long BodyWidth = 7772400L;
    private const long BodyHeight = 4525963L;

    private static SlidePart CreateSlidePart(PresentationPart presentationPart, SlideLayoutPart layoutPart, SlideContent content)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();

        var titleShape = new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 2U, Name = "Title" },
                new P.NonVisualShapeDrawingProperties(new ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Type = PlaceholderValues.Title })),
            new P.ShapeProperties(
                new Transform2D(
                    new A.Offset { X = TitleOffsetX, Y = TitleOffsetY },
                    new A.Extents { Cx = TitleWidth, Cy = TitleHeight }),
                new PresetGeometry(new AdjustValueList()) { Preset = ShapeTypeValues.Rectangle }),
            new P.TextBody(
                new BodyProperties(),
                new ListStyle(),
                new Paragraph(new Run(new RunProperties { Language = "en-US" }, new A.Text(content.Title)))));

        var bodyShape = BuildBodyShape(content.Bullets);

        slidePart.Slide = new Slide(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new TransformGroup(
                        new A.Offset { X = 0L, Y = 0L },
                        new A.Extents { Cx = 9144000L, Cy = 6858000L },
                        new ChildOffset { X = 0L, Y = 0L },
                        new ChildExtents { Cx = 9144000L, Cy = 6858000L })),
                    titleShape,
                    bodyShape)),
            new ColorMapOverride(new MasterColorMapping()));

        slidePart.AddPart(layoutPart);
        return slidePart;
    }

    private static P.Shape BuildBodyShape(List<string> bullets)
    {
        var textBody = new P.TextBody(new BodyProperties(), new ListStyle());
        if (bullets.Count == 0)
        {
            textBody.AppendChild(new Paragraph(new EndParagraphRunProperties { Language = "en-US" }));
        }
        else
        {
            foreach (var bullet in bullets)
            {
                var paragraph = new Paragraph(
                    new ParagraphProperties(new CharacterBullet { Char = "•" }) { Level = 0 },
                    new Run(new RunProperties { Language = "en-US" }, new A.Text(bullet)));
                textBody.AppendChild(paragraph);
            }
        }

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = 3U, Name = "Content" },
                new P.NonVisualShapeDrawingProperties(new ShapeLocks { NoGrouping = true }),
                new ApplicationNonVisualDrawingProperties(new PlaceholderShape { Index = 1U })),
            new P.ShapeProperties(
                new Transform2D(
                    new A.Offset { X = BodyOffsetX, Y = BodyOffsetY },
                    new A.Extents { Cx = BodyWidth, Cy = BodyHeight }),
                new PresetGeometry(new AdjustValueList()) { Preset = ShapeTypeValues.Rectangle }),
            textBody);
    }

    private static SlideMasterPart CreateSlideMasterPart(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var themePart = slideMasterPart.AddNewPart<ThemePart>();
        themePart.Theme = CreateTheme();

        slideMasterPart.SlideMaster = new SlideMaster(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new TransformGroup()))),
            new P.ColorMap
            {
                Background1 = D.ColorSchemeIndexValues.Light1,
                Text1 = D.ColorSchemeIndexValues.Dark1,
                Background2 = D.ColorSchemeIndexValues.Light2,
                Text2 = D.ColorSchemeIndexValues.Dark2,
                Accent1 = D.ColorSchemeIndexValues.Accent1,
                Accent2 = D.ColorSchemeIndexValues.Accent2,
                Accent3 = D.ColorSchemeIndexValues.Accent3,
                Accent4 = D.ColorSchemeIndexValues.Accent4,
                Accent5 = D.ColorSchemeIndexValues.Accent5,
                Accent6 = D.ColorSchemeIndexValues.Accent6,
                Hyperlink = D.ColorSchemeIndexValues.Hyperlink,
                FollowedHyperlink = D.ColorSchemeIndexValues.FollowedHyperlink,
            },
            new SlideLayoutIdList());

        return slideMasterPart;
    }

    private static SlideLayoutPart CreateSlideLayoutPart(SlideMasterPart slideMasterPart)
    {
        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        slideLayoutPart.SlideLayout = new SlideLayout(
            new CommonSlideData(
                new ShapeTree(
                    new P.NonVisualGroupShapeProperties(
                        new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                        new P.NonVisualGroupShapeDrawingProperties(),
                        new ApplicationNonVisualDrawingProperties()),
                    new GroupShapeProperties(new TransformGroup()))),
            new ColorMapOverride(new MasterColorMapping()))
        { Type = SlideLayoutValues.Blank };

        var master = slideMasterPart.SlideMaster!;
        var layoutIdList = master.GetFirstChild<SlideLayoutIdList>() ?? master.AppendChild(new SlideLayoutIdList());
        layoutIdList.AppendChild(new SlideLayoutId
        {
            Id = 2147483649U,
            RelationshipId = slideMasterPart.GetIdOfPart(slideLayoutPart),
        });
        master.Save();

        return slideLayoutPart;
    }

    private static Theme CreateTheme()
    {
        return new Theme(
            new ThemeElements(
                new ColorScheme(
                    new Dark1Color(new SystemColor { Val = SystemColorValues.WindowText, LastColor = "000000" }),
                    new Light1Color(new SystemColor { Val = SystemColorValues.Window, LastColor = "FFFFFF" }),
                    new Dark2Color(new RgbColorModelHex { Val = "1F497D" }),
                    new Light2Color(new RgbColorModelHex { Val = "EEECE1" }),
                    new Accent1Color(new RgbColorModelHex { Val = "4F81BD" }),
                    new Accent2Color(new RgbColorModelHex { Val = "C0504D" }),
                    new Accent3Color(new RgbColorModelHex { Val = "9BBB59" }),
                    new Accent4Color(new RgbColorModelHex { Val = "8064A2" }),
                    new Accent5Color(new RgbColorModelHex { Val = "4BACC6" }),
                    new Accent6Color(new RgbColorModelHex { Val = "F79646" }),
                    new Hyperlink(new RgbColorModelHex { Val = "0000FF" }),
                    new FollowedHyperlinkColor(new RgbColorModelHex { Val = "800080" }))
                { Name = "Office" },
                new FontScheme(
                    new MajorFont(new LatinFont { Typeface = "Calibri" }, new EastAsianFont { Typeface = string.Empty }, new ComplexScriptFont { Typeface = string.Empty }),
                    new MinorFont(new LatinFont { Typeface = "Calibri" }, new EastAsianFont { Typeface = string.Empty }, new ComplexScriptFont { Typeface = string.Empty }))
                { Name = "Office" },
                new FormatScheme(
                    new FillStyleList(
                        new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor }),
                        new GradientFill(new GradientStopList(
                            new GradientStop(new SchemeColor { Val = SchemeColorValues.PhColor }) { Position = 0 },
                            new GradientStop(new SchemeColor { Val = SchemeColorValues.PhColor }) { Position = 100000 }))
                        { RotateWithShape = true },
                        new GradientFill(new GradientStopList(
                            new GradientStop(new SchemeColor { Val = SchemeColorValues.PhColor }) { Position = 0 },
                            new GradientStop(new SchemeColor { Val = SchemeColorValues.PhColor }) { Position = 100000 }))
                        { RotateWithShape = true }),
                    new LineStyleList(
                        new Outline(new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor })),
                        new Outline(new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor })),
                        new Outline(new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor }))),
                    new EffectStyleList(
                        new EffectStyle(new EffectList()),
                        new EffectStyle(new EffectList()),
                        new EffectStyle(new EffectList())),
                    new BackgroundFillStyleList(
                        new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor }),
                        new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor }),
                        new SolidFill(new SchemeColor { Val = SchemeColorValues.PhColor })))
                { Name = "Office" }))
        { Name = "Office Theme" };
    }

    private sealed class SlideContent
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Bullets { get; } = [];
    }
}
