using Sovrant.Api.Ui;

namespace Sovrant.Ui.Tests;

/// <summary>Phase 137 — Web and Desktop draw the same avatar initials for the same person.</summary>
public sealed class AvatarTextTests
{
    [Theory]
    [InlineData("Alex Morgan", "AM")]
    [InlineData("alex.morgan@example.com", "AM")]
    [InlineData("morgan@example.com", "M")]
    [InlineData("morgan", "M")]
    [InlineData("nav-test", "NT")]
    [InlineData("  ada   lovelace byron ", "AL")]
    [InlineData("You", "Y")]
    [InlineData("", "?")]
    [InlineData(null, "?")]
    [InlineData("@example.com", "?")]
    public void Initials_Are_Up_To_Two_Uppercase_Letters(string? name, string expected)
    {
        Assert.Equal(expected, AvatarText.Initials(name));
    }
}
