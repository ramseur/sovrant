using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceMonitorTests : IDisposable
{
    private GovernanceMonitor CreateMonitor(GovernanceLevel level = GovernanceLevel.Strict)
    {
        var config = new GovernanceConfig
        {
            GovernanceLevelName = level.ToString(),
            AuditLog = false, // Disable file I/O in tests
        };
        return new GovernanceMonitor(config, NullLogger<GovernanceMonitor>.Instance);
    }

    public void Dispose() { /* monitors are lightweight */ }

    // --- Pre-execution: Dangerous commands ---

    [Fact]
    public async Task Pre_DangerousCommand_Strict_Blocks()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    [Fact]
    public async Task Pre_DangerousCommand_Standard_Warns()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Standard);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    [Fact]
    public async Task Pre_DangerousCommand_Minimal_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Minimal);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    [Fact]
    public async Task Pre_SafeCommand_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "ls -la");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    // --- Pre-execution: Config protection ---

    [Fact]
    public async Task Pre_ProtectedFile_Strict_Blocks()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: ".editorconfig");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("ConfigProtection", verdict.Rule);
    }

    [Fact]
    public async Task Pre_ProtectedFile_Standard_Warns()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Standard);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: ".editorconfig");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
    }

    [Fact]
    public async Task Pre_RegularFile_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: "src/Foo.cs");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    // --- Pre-execution: Commit quality gate ---

    [Fact]
    public async Task Pre_GitCommit_Strict_Warns()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "git commit -m 'test'");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("CommitQualityGate", verdict.Rule);
    }

    // --- Post-execution: Secret detection ---

    [Fact]
    public async Task Post_SecretInOutput_Strict_Warns()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "Found key: AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("SecretDetection", verdict.Rule);
    }

    [Fact]
    public async Task Post_SecretInInput_Strict_Warns()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolInput: "echo AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("SecretDetection", verdict.Rule);
    }

    [Fact]
    public async Task Post_CleanOutput_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "Build succeeded.");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    [Fact]
    public async Task Post_Secret_Minimal_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Minimal);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    // --- Non-shell/file tools pass through ---

    [Fact]
    public async Task Pre_NonShellTool_Allows()
    {
        using var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Glob", ToolInput: "**/*.cs");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }
}
