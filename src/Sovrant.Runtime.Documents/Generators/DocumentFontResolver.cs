using System.Runtime.InteropServices;
using PdfSharp.Fonts;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// PDFSharp 6 ships without a default font resolver — the <c>Arial</c>
/// face the text generators use has to be mapped to actual font bytes.
/// This resolver probes well-known system locations (Windows, Linux, macOS)
/// and caches the result so subsequent PDFs reuse the same byte buffer.
/// </summary>
/// <remarks>
/// Regular + bold are the only faces needed today. If a future generator
/// requests italic or symbol it will fall back to regular rather than
/// throwing — a PDF with the wrong face beats failing the whole render.
/// </remarks>
internal sealed class DocumentFontResolver : IFontResolver
{
    private static readonly string[] s_regularCandidates =
    [
        @"C:\Windows\Fonts\arial.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
        "/usr/share/fonts/TTF/DejaVuSans.ttf",
        "/Library/Fonts/Arial.ttf",
        "/System/Library/Fonts/Supplemental/Arial.ttf",
    ];

    private static readonly string[] s_boldCandidates =
    [
        @"C:\Windows\Fonts\arialbd.ttf",
        "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
        "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf",
        "/Library/Fonts/Arial Bold.ttf",
        "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    ];

    private static readonly Dictionary<string, byte[]> s_cache = new(StringComparer.Ordinal);
    private static readonly object s_cacheLock = new();

    public byte[]? GetFont(string faceName)
    {
        lock (s_cacheLock)
        {
            if (s_cache.TryGetValue(faceName, out var cached))
                return cached;
        }

        var candidates = faceName.Contains("Bold", StringComparison.Ordinal)
            ? s_boldCandidates
            : s_regularCandidates;

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                lock (s_cacheLock)
                {
                    s_cache[faceName] = bytes;
                }
                return bytes;
            }
        }

        // Fallback to the regular face if a bold lookup failed.
        if (candidates == s_boldCandidates)
        {
            foreach (var path in s_regularCandidates)
            {
                if (File.Exists(path))
                {
                    var bytes = File.ReadAllBytes(path);
                    lock (s_cacheLock)
                    {
                        s_cache[faceName] = bytes;
                    }
                    return bytes;
                }
            }
        }

        throw new FileNotFoundException(
            $"No system font found for face '{faceName}'. Install a base font " +
            $"(Arial on Windows, DejaVu Sans on Linux) or register a custom IFontResolver. " +
            $"Platform: {RuntimeInformation.OSDescription}.");
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var face = isBold ? $"{familyName}#Bold" : familyName;
        return new FontResolverInfo(face);
    }
}
