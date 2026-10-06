using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Sovrant.Runtime.Onboarding;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 140 — the Desktop Welcome overlay, rendered for a member: admin-only areas are described
/// ("Managed by your admin") and nothing on the page can open an admin page.
/// </summary>
public sealed class WelcomeOverlayRenderTests
{
    [AvaloniaFact]
    public async Task Member_Welcome_Shows_Managed_Areas_And_No_Admin_Links()
    {
        var vm = new WelcomeViewModel(new OnboardingService(new ServiceCollection().BuildServiceProvider()));
        await vm.ShowAsync("sam@example.com", "sam", isAdmin: false, workspaceId: null);

        var window = new Window { Width = 1280, Height = 1100, Content = new WelcomeOverlay { DataContext = vm } };
        window.Show();
        try
        {
            window.CaptureRenderedFrame()?.Dispose(); // force layout + templates

            var texts = window.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            Assert.Equal(2, texts.Count(t => t == "Managed by your admin"));
            Assert.Contains("Privacy & governance", texts);

            var targets = window.GetLogicalDescendants().OfType<Button>()
                .Where(b => b.IsEffectivelyVisible && b.CommandParameter is string)
                .Select(b => (string)b.CommandParameter!).ToList();
            Assert.NotEmpty(targets);
            Assert.All(targets, page => Assert.False(MainViewModel.IsAdminOnlyPage(page), page));

            if (Environment.GetEnvironmentVariable("SOVRANT_UI_SHOT") is { Length: > 0 } shot)
                window.CaptureRenderedFrame()?.Save(shot);
        }
        finally
        {
            window.Close();
        }
    }
}
