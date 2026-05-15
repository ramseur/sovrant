using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class RedactionMapTests
{
    [Fact]
    public void SameValue_ReturnsSamePlaceholder()
    {
        var map = new RedactionMap();
        var p1 = map.GetOrCreatePlaceholder("test@example.com", "EMAIL");
        var p2 = map.GetOrCreatePlaceholder("test@example.com", "EMAIL");
        Assert.Equal(p1, p2);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void DifferentValues_GetDifferentPlaceholders()
    {
        var map = new RedactionMap();
        var p1 = map.GetOrCreatePlaceholder("a@b.com", "EMAIL");
        var p2 = map.GetOrCreatePlaceholder("c@d.com", "EMAIL");
        Assert.NotEqual(p1, p2);
        Assert.Equal("[EMAIL_1]", p1);
        Assert.Equal("[EMAIL_2]", p2);
    }

    [Fact]
    public void DifferentCategories_CountIndependently()
    {
        var map = new RedactionMap();
        var email = map.GetOrCreatePlaceholder("a@b.com", "EMAIL");
        var ip = map.GetOrCreatePlaceholder("10.0.0.1", "IP");
        Assert.Equal("[EMAIL_1]", email);
        Assert.Equal("[IP_1]", ip);
    }

    [Fact]
    public void Restore_ReplacesPlaceholders()
    {
        var map = new RedactionMap();
        map.GetOrCreatePlaceholder("a@b.com", "EMAIL");
        map.GetOrCreatePlaceholder("10.0.0.1", "IP");

        var restored = map.Restore("Contact [EMAIL_1] at [IP_1]");
        Assert.Equal("Contact a@b.com at 10.0.0.1", restored);
    }

    [Fact]
    public void Restore_NoPlaceholders_ReturnsOriginal()
    {
        var map = new RedactionMap();
        Assert.Equal("hello world", map.Restore("hello world"));
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        var map = new RedactionMap();
        Assert.True(map.IsEmpty);
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void GetRedactions_ReturnsAllEntries()
    {
        var map = new RedactionMap();
        map.GetOrCreatePlaceholder("secret", "API_KEY");
        var redactions = map.Redactions;
        Assert.Contains("secret", redactions.Keys);
    }
}
