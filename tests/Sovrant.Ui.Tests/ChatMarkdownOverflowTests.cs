using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sovrant.Desktop.Controls;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Known issue "Desktop chat text overflow": text in assistant replies ran past the right edge and
/// was clipped. Renders a finished reply in a narrow window and checks that nothing outside a code
/// block (which scrolls sideways by design) extends past the reply's width.
/// </summary>
public sealed class ChatMarkdownOverflowTests
{
    private const double ReplyWidth = 420;

    private const string Reply = """
        Here is the documentation: https://example.com/a/very/long/path/that/keeps/going/and/going/without/any/spaces/at/all/index.html

        Read [the docs](https://example.com/docs) first, then continue.

        See [the complete configuration reference for self-hosted deployments behind a reverse proxy](https://example.com/docs) for details.

        A long word: Supercalifragilisticexpialidocious_and_then_some_more_characters_with_no_break_opportunity_whatsoever

        Run `C:\Users\someone\AppData\Local\Temp\claude\a-very-long-path\to\a\file\with\no\spaces.txt` to check.

        | Setting | Value |
        |---|---|
        | SOVRANT_TRUSTED_PROXIES | 172.18.0.0/16,10.0.0.5,192.168.100.200/24,fd00::/8 |

        ```
        a code line that is deliberately much longer than the reply width and scrolls sideways inside its own box
        ```
        """;

    [AvaloniaFact]
    public void Finished_Reply_Never_Runs_Past_The_Right_Edge()
    {
        var presenter = new SafeMarkdownPresenter();
        var window = new Window
        {
            Width = ReplyWidth, Height = 900,
            Content = new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = presenter },
        };
        window.Show();
        try
        {
            presenter.Markdown = Reply;
            Dispatcher.UIThread.RunJobs();          // rendering is posted to the dispatcher
            window.CaptureRenderedFrame()?.Dispose(); // force layout

            var overflowing = presenter.GetVisualDescendants()
                .OfType<Control>()
                .Where(c => c.IsEffectivelyVisible && !InsideCodeBlock(c, presenter))
                .Select(c => (Control: c, Right: RightEdge(c, presenter)))
                .Where(x => x.Right > presenter.Bounds.Width + 0.5)
                .Select(x => $"{x.Control.GetType().Name} '{Describe(x.Control)}' right edge {x.Right:F0} > {presenter.Bounds.Width:F0}")
                .ToList();

            if (Environment.GetEnvironmentVariable("SOVRANT_UI_SHOT_DIR") is { Length: > 0 } dir)
                window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "chat-markdown-overflow.png"));

            Assert.True(overflowing.Count == 0, "Overflowing:\n" + string.Join('\n', overflowing.Distinct()));
        }
        finally
        {
            window.Close();
        }
    }

    private static double RightEdge(Control c, Visual root) =>
        c.TranslatePoint(new Point(c.Bounds.Width, 0), root)?.X ?? 0;

    // Code blocks scroll sideways by design: anything inside a ScrollViewer within the reply is exempt.
    private static bool InsideCodeBlock(Control c, Visual root)
    {
        for (var v = c.GetVisualParent(); v is not null && v != root; v = v.GetVisualParent())
            if (v is ScrollViewer) return true;
        return c is ScrollViewer;
    }

    private static string Describe(Control c) => c switch
    {
        TextBlock t => (t.Text ?? string.Join("", t.Inlines?.Select(i => i.GetType().Name) ?? [])) is var s && s.Length > 40 ? s[..40] + "…" : t.Text ?? "",
        ContentControl { Content: string s } => s.Length > 40 ? s[..40] + "…" : s,
        _ => c.Name ?? "",
    };
}
