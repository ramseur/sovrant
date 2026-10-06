using Sovrant.Desktop.ViewModels;
using Sovrant.Runtime.Onboarding;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Phase 140 — Integrations, Trust Boundary / Governance and Workspaces are admin-only today.
/// Members may read about them on the Welcome page, but no Desktop link may lead into them,
/// and the navigation guard must refuse them however the request arrives.
/// </summary>
public class WelcomeAccessTests
{
    [Fact]
    public void Member_Welcome_Links_Never_Resolve_To_An_Admin_Page()
    {
        foreach (var target in Enum.GetValues<WelcomeTarget>())
        {
            var page = WelcomeViewModel.PageFor(target, isAdmin: false);
            if (page is not null)
                Assert.False(MainViewModel.IsAdminOnlyPage(page), $"{target} → {page} is an admin page");
        }
    }

    [Fact]
    public void Admin_Welcome_Links_Resolve_To_Their_Pages()
    {
        Assert.Equal("AdminPlatformIntegrations", WelcomeViewModel.PageFor(WelcomeTarget.Integrations, isAdmin: true));
        Assert.Equal("TrustBoundary", WelcomeViewModel.PageFor(WelcomeTarget.TrustBoundary, isAdmin: true));
        Assert.Equal("AdminWorkspaces", WelcomeViewModel.PageFor(WelcomeTarget.Workspaces, isAdmin: true));
    }

    [Fact]
    public void Member_Chat_Capability_Cards_Never_Open_An_Admin_Page()
    {
        var member = ChatViewModel.BuildCapabilities(isAdmin: false);
        Assert.Equal(6, member.Count);
        Assert.All(member, c => Assert.False(MainViewModel.IsAdminOnlyPage(c.Page), c.Title));

        var admin = ChatViewModel.BuildCapabilities(isAdmin: true);
        Assert.Contains(admin, c => c.Page == "TrustBoundary");
    }

    [Theory]
    [InlineData("Integrations")]
    [InlineData("AdminPlatformIntegrations")]
    [InlineData("AdminSystemIntegrations")]
    [InlineData("TrustBoundary")]
    [InlineData("Governance")]
    [InlineData("AdminWorkspaces")]
    [InlineData("AdminProviders")]
    [InlineData("Admin")]
    [InlineData("Diagnostics")]
    [InlineData("CommandCenter")]
    public void Admin_Pages_Are_Guarded(string page) => Assert.True(MainViewModel.IsAdminOnlyPage(page));
}
