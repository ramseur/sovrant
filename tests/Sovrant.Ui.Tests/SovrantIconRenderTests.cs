using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Sovrant.Api.Ui;
using Sovrant.Desktop.Controls;

[assembly: AvaloniaTestApplication(typeof(Sovrant.Ui.Tests.HeadlessTestApp))]
// The headless Avalonia render tests share one UI thread; run in parallel they occasionally deadlock
// (seen as a test host hanging forever). The whole project takes a few seconds, so run tests one at a time.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 136 — Lucide.Avalonia is built against Avalonia 11.3 and only verified on our
/// Avalonia 12. Rendering a real icon headlessly catches an Avalonia upgrade that breaks it
/// (a sibling package, LucideAvalonia, throws MissingMethodException on 12; another renders blank).
/// </summary>
public sealed class SovrantIconRenderTests
{
    [AvaloniaFact]
    public void SovrantIcon_Draws_Its_Glyph()
    {
        var lit = RenderLitPixels(new SovrantIcon { IconName = IconNames.Agents, Size = 48, Foreground = Brushes.White });
        Assert.True(lit > 100, $"expected the glyph to draw, got {lit} lit pixels");
    }

    [AvaloniaFact]
    public void SovrantIcon_Without_A_Name_Draws_Nothing()
    {
        Assert.Equal(0, RenderLitPixels(new SovrantIcon { Size = 48, Foreground = Brushes.White }));
    }

    [AvaloniaFact]
    public void SovrantIcon_Inherits_Foreground_From_Its_Parent()
    {
        var icon = new SovrantIcon { IconName = IconNames.Close, Size = 48 };
        var lit = RenderLitPixels(new Border { Child = icon }, parentForeground: Brushes.White);
        Assert.True(lit > 50, $"expected the inherited foreground to draw the glyph, got {lit} lit pixels");
    }

    [AvaloniaFact]
    public void IconName_Sets_The_Mapped_Glyph_And_Default_Stroke()
    {
        var icon = new SovrantIcon { IconName = IconNames.Delete };
        Assert.Equal(SovrantIconMap.Map[IconNames.Delete], icon.Kind);
        Assert.Equal(SovrantIconMap.DefaultStrokeWidth, icon.StrokeWidth);
        icon.IconName = null;
        Assert.Null(icon.Kind);
    }

    private static int RenderLitPixels(Control content, IBrush? parentForeground = null)
    {
        var window = new Window { Width = 96, Height = 96, Background = Brushes.Black, Content = content };
        if (parentForeground is not null)
            window.Foreground = parentForeground;
        window.Show();
        try
        {
            using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame rendered");
            return CountLitPixels(frame);
        }
        finally
        {
            window.Close();
        }
    }

    private static int CountLitPixels(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var buffer = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }
        var lit = 0;
        for (var i = 0; i < buffer.Length; i += 4)
            if (buffer[i] + buffer[i + 1] + buffer[i + 2] > 300)
                lit++;
        return lit;
    }
}

public sealed class HeadlessTestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
