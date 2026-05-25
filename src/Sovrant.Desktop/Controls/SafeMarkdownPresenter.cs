using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Sovrant.Desktop.Controls;

/// <summary>
/// Renders markdown to native Avalonia controls using Markdig's AST.
/// Handles headings, paragraphs, code blocks, inline code, lists, and bold/italic.
/// Falls back to plain text if parsing fails.
/// </summary>
public class SafeMarkdownPresenter : ContentControl
{
    private int _renderVersion;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<SafeMarkdownPresenter, string?>(nameof(Markdown));

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    static SafeMarkdownPresenter()
    {
        MarkdownProperty.Changed.AddClassHandler<SafeMarkdownPresenter>(
            (presenter, _) => presenter.RenderMarkdownAsync());
    }

    private void RenderMarkdownAsync()
    {
        var text = Markdown;
        if (string.IsNullOrEmpty(text))
        {
            Content = null;
            return;
        }

        var version = ++_renderVersion;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != _renderVersion) return;

            try
            {
                var doc = Markdig.Markdown.Parse(text, Pipeline);
                var panel = new StackPanel { Spacing = 6 };

                foreach (var block in doc)
                {
                    var control = RenderBlock(block);
                    if (control is not null)
                        panel.Children.Add(control);
                }

                if (version == _renderVersion)
                    Content = panel;
            }
            catch
            {
                if (version == _renderVersion)
                    Content = CreateFallback(text);
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Content is null && !string.IsNullOrEmpty(Markdown))
            RenderMarkdownAsync();
    }

    private IBrush ThemeBrush(string resourceKey, string fallbackHex)
    {
        if (this.TryFindResource(resourceKey, out var val) && val is IBrush brush)
            return brush;
        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private Control? RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch
                {
                    1 => 22.0,
                    2 => 18.0,
                    3 => 16.0,
                    _ => 15.0,
                };
                var headerTb = new SelectableTextBlock
                {
                    FontSize = fontSize,
                    FontWeight = FontWeight.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrush("TextPrimary", "#E0E0E0"),
                    Margin = new Thickness(0, 4, 0, 2),
                };
                SetInlines(headerTb, heading.Inline);
                return headerTb;

            case ParagraphBlock paragraph:
                var paraTb = new SelectableTextBlock
                {
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ThemeBrush("TextPrimary", "#E0E0E0"),
                    Margin = new Thickness(0, 2, 0, 2),
                };
                SetInlines(paraTb, paragraph.Inline);
                return paraTb;

            case FencedCodeBlock fencedCode:
                var codeText = GetCodeBlockText(fencedCode);
                var langLabel = !string.IsNullOrEmpty(fencedCode.Info) ? fencedCode.Info : null;
                return CreateCodeBlock(codeText, langLabel);

            case CodeBlock codeBlock:
                return CreateCodeBlock(GetCodeBlockText(codeBlock), null);

            case ListBlock list:
                return RenderList(list);

            case ThematicBreakBlock:
                return new Border
                {
                    Height = 1,
                    Background = ThemeBrush("SurfaceBorder", "#3A3A3A"),
                    Margin = new Thickness(0, 8),
                };

            case QuoteBlock quote:
                var quotePanel = new StackPanel { Spacing = 4 };
                foreach (var child in quote)
                {
                    var c = RenderBlock(child);
                    if (c is not null) quotePanel.Children.Add(c);
                }
                return new Border
                {
                    BorderBrush = ThemeBrush("BrandPrimary", "#6D52C6"),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 4, 0, 4),
                    Child = quotePanel,
                    Margin = new Thickness(0, 4),
                };

            default:
                return null;
        }
    }

    private Border CreateCodeBlock(string code, string? language)
    {
        var panel = new StackPanel();

        // Header row: language label + copy button
        var headerRow = new DockPanel { Margin = new Thickness(12, 6, 8, 0) };

        if (language is not null)
        {
            headerRow.Children.Add(new TextBlock
            {
                Text = language,
                FontSize = 11,
                Foreground = ThemeBrush("TextSecondary", "#999999"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        var copyButton = new Button
        {
            Content = "Copy",
            FontSize = 10,
            Padding = new Thickness(8, 2),
            Background = ThemeBrush("SurfaceHover", "#2A2A2A"),
            Foreground = ThemeBrush("TextSecondary", "#AAAAAA"),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        DockPanel.SetDock(copyButton, Dock.Right);
        headerRow.Children.Insert(0, copyButton);

        // Capture code text for the click handler
        var codeText = code;
        copyButton.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(copyButton)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(codeText);
                ((Button)copyButton).Content = "Copied!";
                await Task.Delay(1500);
                ((Button)copyButton).Content = "Copy";
            }
        };

        panel.Children.Add(headerRow);

        panel.Children.Add(new SelectableTextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas,Menlo,monospace"),
            FontSize = 12,
            Foreground = ThemeBrush("TextPrimary", "#E0E0E0"),
            TextWrapping = TextWrapping.NoWrap,
            Padding = new Thickness(12, 8),
        });

        return new Border
        {
            Background = ThemeBrush("CodeBlockBackground", "#111111"),
            BorderBrush = ThemeBrush("SurfaceBorder", "#3A3A3A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 4),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = panel,
            },
        };
    }

    private Border RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4) };
        int index = 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            var bullet = list.IsOrdered ? $"{index++}." : "\u2022";
            var bulletWidth = list.IsOrdered ? 24 : 16;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(bulletWidth)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var bulletBlock = new TextBlock
            {
                Text = bullet,
                FontSize = 14,
                Foreground = ThemeBrush("TextSecondary", "#999999"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 6, 0),
            };
            Grid.SetColumn(bulletBlock, 0);
            grid.Children.Add(bulletBlock);

            var contentPanel = new StackPanel { Spacing = 2 };
            Grid.SetColumn(contentPanel, 1);
            foreach (var child in listItem)
            {
                var c = RenderBlock(child);
                if (c is not null) contentPanel.Children.Add(c);
            }
            grid.Children.Add(contentPanel);
            panel.Children.Add(grid);
        }

        return new Border
        {
            Padding = new Thickness(16, 0, 0, 0),
            Child = panel,
        };
    }

    private void SetInlines(SelectableTextBlock tb, ContainerInline? inlines)
    {
        if (inlines is null)
        {
            tb.Text = string.Empty;
            return;
        }

        var avInlines = new Avalonia.Controls.Documents.InlineCollection();

        foreach (var inline in inlines)
        {
            AddInline(avInlines, inline);
        }

        tb.Inlines = avInlines;
    }

    private void AddInline(Avalonia.Controls.Documents.InlineCollection inlines, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                inlines.Add(new Avalonia.Controls.Documents.Run(literal.Content.ToString()));
                break;

            case EmphasisInline emphasis:
                foreach (var child in emphasis)
                {
                    // Bold+italic or just one
                    if (child is LiteralInline lit)
                    {
                        var run = new Avalonia.Controls.Documents.Run(lit.Content.ToString());
                        if (emphasis.DelimiterCount >= 2)
                            run.FontWeight = FontWeight.Bold;
                        else
                            run.FontStyle = FontStyle.Italic;
                        inlines.Add(run);
                    }
                    else
                    {
                        AddInline(inlines, child);
                    }
                }
                break;

            case CodeInline code:
                var codeRun = new Avalonia.Controls.Documents.Run(code.Content)
                {
                    FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                    Background = ThemeBrush("SurfaceHover", "#2A2A2A"),
                    Foreground = ThemeBrush("TextPrimary", "#E0E0E0"),
                };
                inlines.Add(codeRun);
                break;

            case LineBreakInline:
                inlines.Add(new Avalonia.Controls.Documents.LineBreak());
                break;

            case LinkInline link:
            {
                var url = link.Url ?? "";
                var textSb = new System.Text.StringBuilder();
                foreach (var child in link)
                    if (child is LiteralInline lt) textSb.Append(lt.Content.ToString());
                var displayText = textSb.Length > 0 ? textSb.ToString() : url;
                if (link.IsImage) displayText = "🖼 " + displayText;
                inlines.Add(MakeLinkInline(displayText, url));
                break;
            }

            case AutolinkInline autolink:
                inlines.Add(MakeLinkInline(autolink.Url, autolink.Url));
                break;

            default:
                // For any other inline, try to get its text content
                if (inline is ContainerInline container)
                {
                    foreach (var child in container)
                        AddInline(inlines, child);
                }
                break;
        }
    }

    private Avalonia.Controls.Documents.Inline MakeLinkInline(string displayText, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return new Avalonia.Controls.Documents.Run(displayText)
            {
                Foreground = ThemeBrush("BrandPrimary", "#6D52C6"),
            };
        }

        var btn = new Button
        {
            Content = displayText,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = ThemeBrush("BrandPrimary", "#6D52C6"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.Click += (_, _) => OpenUrl(url);
        return new Avalonia.Controls.Documents.InlineUIContainer { Child = btn };
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private static string GetCodeBlockText(LeafBlock codeBlock)
    {
        var lines = codeBlock.Lines;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines.Lines[i];
            if (i > 0) sb.AppendLine();
            sb.Append(line.Slice.AsSpan());
        }
        // Trim trailing newline
        while (sb.Length > 0 && sb[sb.Length - 1] is '\r' or '\n')
            sb.Length--;
        return sb.ToString();
    }

    private SelectableTextBlock CreateFallback(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        FontSize = 14,
        Foreground = ThemeBrush("TextPrimary", "#E0E0E0"),
    };
}
