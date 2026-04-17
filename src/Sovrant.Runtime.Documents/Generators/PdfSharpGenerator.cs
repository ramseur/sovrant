using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Renders the request body as a plain text PDF using PDFSharp. Intended
/// for the simple-text case (log dumps, raw responses). Use
/// <see cref="MigraDocGenerator"/> for anything with structure.
/// </summary>
public sealed class PdfSharpGenerator : DocumentGeneratorBase
{
    private const double MarginPoints = 54.0;
    private const double BodyFontSize = 10.0;
    private const double TitleFontSize = 16.0;
    private const double LineHeight = 14.0;
    private const string BodyFontFamily = "Arial";

    private static readonly object s_fontResolverLock = new();
    private static bool s_fontResolverInitialised;

    public PdfSharpGenerator(IArtifactStore artifactStore) : base(artifactStore)
    {
        EnsureFontResolver();
    }

    public override DocumentFormat Format => DocumentFormat.Pdf;

    protected override string ContentType => "application/pdf";

    protected override Task<byte[]> RenderAsync(DocumentRequest request, CancellationToken ct)
    {
        using var document = new PdfDocument();
        document.Info.Title = request.Title ?? Path.GetFileNameWithoutExtension(request.FileName);
        document.Info.Creator = "Sovrant";

        var titleFont = new XFont(BodyFontFamily, TitleFontSize, XFontStyleEx.Bold);
        var bodyFont = new XFont(BodyFontFamily, BodyFontSize, XFontStyleEx.Regular);

        PdfPage page = AddPage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);

        double y = MarginPoints;
        double maxWidth = page.Width.Point - (MarginPoints * 2);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            gfx.DrawString(request.Title, titleFont, XBrushes.Black,
                new XPoint(MarginPoints, y));
            y += TitleFontSize + 10;
        }

        var body = request.Body ?? string.Empty;
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var rawLine in lines)
        {
            ct.ThrowIfCancellationRequested();

            var wrapped = WrapLine(gfx, rawLine, bodyFont, maxWidth);
            foreach (var visual in wrapped)
            {
                if (y + LineHeight > page.Height.Point - MarginPoints)
                {
                    gfx.Dispose();
                    page = AddPage(document);
                    gfx = XGraphics.FromPdfPage(page);
                    y = MarginPoints;
                }

                gfx.DrawString(visual, bodyFont, XBrushes.Black,
                    new XPoint(MarginPoints, y));
                y += LineHeight;
            }
        }

        gfx.Dispose();

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return Task.FromResult(ms.ToArray());
    }

    private static PdfPage AddPage(PdfDocument document)
    {
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.Letter;
        return page;
    }

    private static IEnumerable<string> WrapLine(XGraphics gfx, string line, XFont font, double maxWidth)
    {
        if (string.IsNullOrEmpty(line))
        {
            yield return string.Empty;
            yield break;
        }

        var words = line.Split(' ');
        var current = new System.Text.StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            var width = gfx.MeasureString(candidate, font).Width;

            if (width > maxWidth && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
                current.Append(word);
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
        }

        if (current.Length > 0)
            yield return current.ToString();
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
