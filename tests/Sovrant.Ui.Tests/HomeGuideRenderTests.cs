using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using Sovrant.Desktop.ViewModels;
using Sovrant.Desktop.Views;
using Sovrant.Runtime.Onboarding;
using Sovrant.Runtime.Preferences;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 141 — Home's guide on Desktop: first visit greets with "Welcome to Sovrant" (once per user),
/// later visits use the time-of-day greeting; members see admin-managed cards with no links; a
/// finished checklist collapses to "All set" and stays dismissed.
/// </summary>
public sealed class HomeGuideRenderTests
{
    [AvaloniaFact]
    public async Task Member_Home_Guide_Shows_Managed_Cards_And_No_Admin_Links()
    {
        var vm = new HomeGuideViewModel(Onboarding(new MemoryPrefs()));
        await vm.LoadAsync("sam@example.com", "sam", isAdmin: false, workspaceId: null, localHour: 9);

        var window = new Window { Width = 1200, Height = 1000, Content = new HomeGuideView { DataContext = vm } };
        window.Show();
        try
        {
            window.CaptureRenderedFrame()?.Dispose();
            var texts = window.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            Assert.Equal(2, texts.Count(t => t == "Managed by your admin"));
            Assert.Contains("What Sovrant can do", texts);
            Assert.Contains("Get started", texts);
            Assert.True(vm.ShowPill);

            var targets = window.GetLogicalDescendants().OfType<Button>()
                .Where(b => b.IsEffectivelyVisible && b.CommandParameter is string)
                .Select(b => (string)b.CommandParameter!).ToList();
            Assert.NotEmpty(targets);
            Assert.All(targets, page => Assert.False(MainViewModel.IsAdminOnlyPage(page), page));

            if (Environment.GetEnvironmentVariable("SOVRANT_UI_SHOT_DIR") is { Length: > 0 } dir)
                window.CaptureRenderedFrame()?.Save(Path.Combine(dir, "home-guide-member.png"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task First_Visit_Greets_Once_Per_User_Then_Time_Of_Day()
    {
        var prefs = new MemoryPrefs();
        var vm = new HomeGuideViewModel(Onboarding(prefs));

        await vm.LoadAsync("sam@example.com", "sam", false, null, localHour: 15);
        Assert.Equal("Welcome to Sovrant, sam", vm.Greeting);
        await vm.LoadAsync("sam@example.com", "sam", false, null, localHour: 15); // 30 s refresh, same session
        Assert.Equal("Welcome to Sovrant, sam", vm.Greeting);

        vm.Reset(); // sign out and back in
        await vm.LoadAsync("sam@example.com", "sam", false, null, localHour: 15);
        Assert.Equal("Good afternoon, sam", vm.Greeting);
        await vm.LoadAsync("sam@example.com", "sam", false, null, localHour: 20);
        Assert.Equal("Good evening, sam", vm.Greeting);
        await vm.LoadAsync("sam@example.com", "sam", false, null, localHour: 7);
        Assert.Equal("Good morning, sam", vm.Greeting);
    }

    [AvaloniaFact]
    public async Task Completed_Checklist_Collapses_To_All_Set_And_Stays_Dismissed()
    {
        var prefs = new MemoryPrefs();
        // A member whose checklist is fully done: model picked + conversation + agent + knowledge.
        var services = new ServiceCollection().AddSingleton<IUserPreferenceStore>(prefs).BuildServiceProvider();
        var vm = new HomeGuideViewModel(new OnboardingService(services));
        await vm.LoadAsync("sam@example.com", "sam", false, null, 9);

        // Without stores the member items can't all be ticked, so drive the completed state directly.
        vm.IsComplete = true;
        Assert.False(vm.ShowPill);
        Assert.False(vm.ShowChecklist);
        Assert.True(vm.ShowAllSet);

        await vm.DismissCommand.ExecuteAsync(null);
        Assert.False(vm.ShowAllSet);
        Assert.Equal("true", await prefs.GetAsync("sam@example.com", UserPreferenceKeys.GetStartedDismissed));

        var again = new HomeGuideViewModel(new OnboardingService(services));
        await again.LoadAsync("sam@example.com", "sam", false, null, 9);
        Assert.True(again.IsDismissed);
    }

    private static OnboardingService Onboarding(IUserPreferenceStore prefs) =>
        new(new ServiceCollection().AddSingleton(prefs).BuildServiceProvider());

    private sealed class MemoryPrefs : IUserPreferenceStore
    {
        private readonly ConcurrentDictionary<(string, string), string> _values = new();

        public Task<string?> GetAsync(string userId, string key, CancellationToken ct = default) =>
            Task.FromResult(_values.TryGetValue((userId, key), out var v) ? v : null);

        public Task SetAsync(string userId, string key, string value, CancellationToken ct = default)
        {
            _values[(userId, key)] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string userId, string key, CancellationToken ct = default)
        {
            _values.TryRemove((userId, key), out _);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(_values.Where(kv => kv.Key.Item1 == userId)
                .ToDictionary(kv => kv.Key.Item2, kv => kv.Value));
    }
}
