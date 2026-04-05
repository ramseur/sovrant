using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-gov-cfg-{Guid.NewGuid():N}");

    public GovernanceConfigTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var config = GovernanceConfig.Load(_tempDir);

        Assert.Equal(GovernanceLevel.Standard, config.Level);
        Assert.True(config.AuditLog);
        Assert.Empty(config.BlockedCommands);
        Assert.Empty(config.ProtectedFiles);
        Assert.Empty(config.SecretPatterns);
    }

    [Fact]
    public void Load_ValidJson_ParsesAllFields()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), """
        {
            "governance_level": "strict",
            "blocked_commands": ["rm -rf /home"],
            "protected_files": ["*.lock"],
            "secret_patterns": ["MY_SECRET_[0-9]+"],
            "audit_log": false
        }
        """);

        var config = GovernanceConfig.Load(_tempDir);

        Assert.Equal(GovernanceLevel.Strict, config.Level);
        Assert.False(config.AuditLog);
        Assert.Single(config.BlockedCommands);
        Assert.Contains("rm -rf /home", config.BlockedCommands);
        Assert.Single(config.ProtectedFiles);
        Assert.Single(config.SecretPatterns);
    }

    [Fact]
    public void Load_InvalidJson_ReturnsDefaults()
    {
        var dir = Path.Combine(_tempDir, ".sovrant");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "governance.json"), "not json{{{");

        var config = GovernanceConfig.Load(_tempDir);
        Assert.Equal(GovernanceLevel.Standard, config.Level);
    }

    [Fact]
    public void Level_InvalidName_DefaultsToStandard()
    {
        var config = new GovernanceConfig { GovernanceLevelName = "invalid" };
        Assert.Equal(GovernanceLevel.Standard, config.Level);
    }

    [Fact]
    public void Level_CaseInsensitive()
    {
        var config = new GovernanceConfig { GovernanceLevelName = "MINIMAL" };
        Assert.Equal(GovernanceLevel.Minimal, config.Level);
    }
}
