using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Markdown.Avalonia;

namespace Sovrant.Desktop.Controls;

/// <summary>
/// A safe wrapper around <see cref="MarkdownScrollViewer"/> that catches rendering
/// crashes (common with code fences, backticks, and certain markdown patterns) and
/// falls back to a plain <see cref="SelectableTextBlock"/>.
/// Renders asynchronously to avoid freezing the UI on long responses.
/// </summary>
public class SafeMarkdownPresenter : ContentControl
{
    private int _renderVersion;

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

        // Show plain text immediately so the UI doesn't go blank.
        Content = CreateFallback(text);

        // Bump version to cancel stale renders.
        var version = ++_renderVersion;

        // Render markdown on a background dispatcher priority so it doesn't block input.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Stale — a newer render was requested.
            if (version != _renderVersion) return;

            try
            {
                var viewer = new MarkdownScrollViewer
                {
                    Markdown = text,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Top,
                };

                // Force a measure to trigger any parse crash early.
                viewer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                if (version == _renderVersion)
                    Content = viewer;
            }
            catch
            {
                // Markdown rendering crashed — keep the plain text fallback already shown.
            }
        }, DispatcherPriority.Background);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Content is null && !string.IsNullOrEmpty(Markdown))
            RenderMarkdownAsync();
    }

    private static SelectableTextBlock CreateFallback(string text)
    {
        // Strip markdown fences for cleaner plain-text display.
        var cleaned = text
            .Replace("```csharp", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```python", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```xml", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```bash", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```shell", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```markdown", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```yaml", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "", StringComparison.Ordinal);

        return new SelectableTextBlock
        {
            Text = cleaned,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#E0E0E0")),
        };
    }
}
