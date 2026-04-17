using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Sovrant.Runtime.Documents.Generators;

/// <summary>
/// Minimal Markdown AST projection shared by the Word/PowerPoint/MigraDoc
/// generators. Markdig gives us everything we need but re-walking its tree
/// for each backend means three copies of the same inline-extraction
/// boilerplate. Instead, flatten to this neutral shape once.
/// </summary>
internal static class MarkdownModel
{
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static IReadOnlyList<MarkdownBlock> Parse(string body)
    {
        var document = Markdown.Parse(body ?? string.Empty, s_pipeline);
        var blocks = new List<MarkdownBlock>(document.Count);
        foreach (var block in document)
            blocks.Add(Project(block));
        return blocks;
    }

    private static MarkdownBlock Project(Block block) => block switch
    {
        HeadingBlock h => new MarkdownBlock(MarkdownBlockKind.Heading, Math.Clamp(h.Level, 1, 6),
            ProjectInlines(h.Inline), null, false),
        ParagraphBlock p => new MarkdownBlock(MarkdownBlockKind.Paragraph, 0,
            ProjectInlines(p.Inline), null, false),
        FencedCodeBlock fc => new MarkdownBlock(MarkdownBlockKind.CodeBlock, 0,
            [], string.Join('\n', fc.Lines.Lines.Select(l => l.ToString())), false),
        CodeBlock cb => new MarkdownBlock(MarkdownBlockKind.CodeBlock, 0,
            [], string.Join('\n', cb.Lines.Lines.Select(l => l.ToString())), false),
        ListBlock list => FlattenList(list),
        QuoteBlock quote => new MarkdownBlock(MarkdownBlockKind.Quote, 0,
            FlattenQuoteInlines(quote), null, false),
        ThematicBreakBlock => new MarkdownBlock(MarkdownBlockKind.ThematicBreak, 0, [], null, false),
        _ => new MarkdownBlock(MarkdownBlockKind.Paragraph, 0,
            [new MarkdownRun(block.ToString() ?? string.Empty, false, false, false)], null, false),
    };

    private static MarkdownBlock FlattenList(ListBlock list)
    {
        var items = new List<IReadOnlyList<MarkdownRun>>();
        foreach (var child in list)
        {
            if (child is not ListItemBlock itemBlock) continue;
            var runs = new List<MarkdownRun>();
            foreach (var sub in itemBlock)
            {
                if (sub is ParagraphBlock para)
                    runs.AddRange(ProjectInlines(para.Inline));
            }
            items.Add(runs);
        }
        return new MarkdownBlock(
            MarkdownBlockKind.List,
            list.IsOrdered ? 1 : 0,
            [],
            null,
            list.IsOrdered,
            items);
    }

    private static List<MarkdownRun> FlattenQuoteInlines(QuoteBlock quote)
    {
        var runs = new List<MarkdownRun>();
        foreach (var inner in quote)
        {
            if (inner is ParagraphBlock p)
                runs.AddRange(ProjectInlines(p.Inline));
        }
        return runs;
    }

    private static IReadOnlyList<MarkdownRun> ProjectInlines(ContainerInline? inlines)
    {
        if (inlines is null) return Array.Empty<MarkdownRun>();
        var runs = new List<MarkdownRun>();
        CollectInlines(inlines, bold: false, italic: false, code: false, runs);
        return runs;
    }

    private static void CollectInlines(ContainerInline container, bool bold, bool italic, bool code, List<MarkdownRun> runs)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    runs.Add(new MarkdownRun(lit.Content.ToString(), bold, italic, code));
                    break;
                case EmphasisInline emph:
                    var isBold = emph.DelimiterCount >= 2;
                    CollectInlines(emph, bold || isBold, italic || !isBold, code, runs);
                    break;
                case CodeInline ci:
                    runs.Add(new MarkdownRun(ci.Content, bold, italic, Code: true));
                    break;
                case LineBreakInline:
                    runs.Add(new MarkdownRun("\n", bold, italic, code));
                    break;
                case LinkInline link:
                    CollectInlines(link, bold, italic, code, runs);
                    break;
                case ContainerInline other:
                    CollectInlines(other, bold, italic, code, runs);
                    break;
                default:
                    runs.Add(new MarkdownRun(inline.ToString() ?? string.Empty, bold, italic, code));
                    break;
            }
        }
    }
}

internal enum MarkdownBlockKind
{
    Heading,
    Paragraph,
    CodeBlock,
    List,
    Quote,
    ThematicBreak,
}

internal sealed record MarkdownBlock(
    MarkdownBlockKind Kind,
    int Level,
    IReadOnlyList<MarkdownRun> Inlines,
    string? RawText,
    bool Ordered,
    IReadOnlyList<IReadOnlyList<MarkdownRun>>? ListItems = null);

internal sealed record MarkdownRun(string Text, bool Bold, bool Italic, bool Code);
