using Sovrant.Api.Ui;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Phase 136 — catalog icons are <see cref="IconNames"/> values (category icons until
/// licensed brand logos exist), never emoji, so Web and Desktop can render them with SovrantIcon.
/// </summary>
public sealed class IntegrationCatalogIconTests
{
    [Fact]
    public void Every_Entry_Uses_An_Integration_Category_Icon()
    {
        var categories = new[]
        {
            IconNames.IntegrationAutomation, IconNames.IntegrationPlatform, IconNames.IntegrationDatabase,
            IconNames.IntegrationSearch, IconNames.IntegrationDxp,
        };
        Assert.All(IntegrationCatalog.All, e => Assert.Contains(e.Icon, categories));
    }

    [Theory]
    [InlineData("github", IconNames.IntegrationPlatform)]
    [InlineData("postgres", IconNames.IntegrationDatabase)]
    [InlineData("tavily", IconNames.IntegrationSearch)]
    [InlineData("zapier", IconNames.IntegrationAutomation)]
    [InlineData("aem", IconNames.IntegrationDxp)]
    public void Entries_Map_To_Their_Kind(string id, string icon)
    {
        Assert.Equal(icon, IntegrationCatalog.All.Single(e => e.Id == id).Icon);
    }
}
