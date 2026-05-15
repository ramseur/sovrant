using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class ConfigProtectionRuleTests
{
    private readonly ConfigProtectionRule _rule = new();

    [Fact]
    public void IsProtected_Null_ReturnsFalse() => Assert.False(_rule.IsProtected(null));

    [Fact]
    public void IsProtected_Empty_ReturnsFalse() => Assert.False(_rule.IsProtected(""));

    [Fact]
    public void IsProtected_Editorconfig_ReturnsTrue() =>
        Assert.True(_rule.IsProtected(".editorconfig"));

    [Fact]
    public void IsProtected_Eslintrc_ReturnsTrue() =>
        Assert.True(_rule.IsProtected(".eslintrc"));

    [Fact]
    public void IsProtected_EslintrcJson_ReturnsTrue() =>
        Assert.True(_rule.IsProtected(".eslintrc.json"));

    [Fact]
    public void IsProtected_DirectoryBuildProps_ReturnsTrue() =>
        Assert.True(_rule.IsProtected("Directory.Build.props"));

    [Fact]
    public void IsProtected_DirectoryBuildTargets_ReturnsTrue() =>
        Assert.True(_rule.IsProtected("Directory.Build.targets"));

    [Fact]
    public void IsProtected_GlobalJson_ReturnsTrue() =>
        Assert.True(_rule.IsProtected("global.json"));

    [Fact]
    public void IsProtected_NuGetConfig_ReturnsTrue() =>
        Assert.True(_rule.IsProtected("NuGet.Config"));

    [Fact]
    public void IsProtected_GithubWorkflow_ReturnsTrue() =>
        Assert.True(_rule.IsProtected(".github/workflows/ci.yml"));

    [Fact]
    public void IsProtected_RegularFile_ReturnsFalse() =>
        Assert.False(_rule.IsProtected("src/Foo.cs"));

    [Fact]
    public void IsProtected_RegularJson_ReturnsFalse() =>
        Assert.False(_rule.IsProtected("appsettings.json"));

    [Fact]
    public void IsProtected_FullPath_MatchesFilename() =>
        Assert.True(_rule.IsProtected("C:\\project\\.editorconfig"));

    [Fact]
    public void IsProtected_CustomPattern_Detects()
    {
        var rule = new ConfigProtectionRule(["*.secret"]);
        Assert.True(rule.IsProtected("config.secret"));
        Assert.False(rule.IsProtected("config.txt"));
    }
}
