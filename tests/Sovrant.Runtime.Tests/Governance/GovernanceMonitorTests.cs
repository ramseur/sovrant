using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class GovernanceMonitorTests
{
    private static GovernanceMonitor CreateMonitor(GovernanceLevel level = GovernanceLevel.Strict)
    {
        var config = new GovernanceConfig
        {
            GovernanceLevelName = level.ToString(),
            AuditLog = false, // Disable file I/O in tests
        };
        return new GovernanceMonitor(LiveSettings.Static(config), NullAuditStore.Instance, NullLogger<GovernanceMonitor>.Instance);
    }

    // --- Pre-execution: Dangerous commands ---

    [Fact]
    public async Task Pre_DangerousCommand_Strict_Blocks()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    [Fact]
    public async Task Pre_DangerousCommand_Standard_Warns()
    {
        var monitor = CreateMonitor(GovernanceLevel.Standard);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    [Fact]
    public async Task Pre_DangerousCommand_Minimal_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Minimal);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    [Fact]
    public async Task Pre_SafeCommand_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "ls -la");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    [Fact]
    public async Task HotReload_RefreshesBlockedCommands()
    {
        GovernanceConfig MakeConfig(params string[] blocked)
        {
            var c = new GovernanceConfig
            {
                GovernanceLevelName = nameof(GovernanceLevel.Strict),
                AuditLog = false,
            };
            foreach (var b in blocked) c.BlockedCommands.Add(b);
            return c;
        }

        var current = MakeConfig();
        var live = new LiveSettings<GovernanceConfig>(() => current);
        using var monitor = new GovernanceMonitor(live, NullAuditStore.Instance, NullLogger<GovernanceMonitor>.Instance);

        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "deploy --to-prod");
        Assert.Equal(GovernanceAction.Allow, (await monitor.EvaluateAsync(ctx)).Action);

        current = MakeConfig("deploy --to-prod");
        live.Reload();

        var verdict = await monitor.EvaluateAsync(ctx);
        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("DangerousCommand", verdict.Rule);
    }

    // --- Pre-execution: Config protection ---

    [Fact]
    public async Task Pre_ProtectedFile_Strict_Blocks()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: ".editorconfig");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Block, verdict.Action);
        Assert.Equal("ConfigProtection", verdict.Rule);
    }

    [Fact]
    public async Task Pre_ProtectedFile_Standard_Warns()
    {
        var monitor = CreateMonitor(GovernanceLevel.Standard);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: ".editorconfig");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
    }

    [Fact]
    public async Task Pre_RegularFile_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Edit", FilePath: "src/Foo.cs");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    // --- Pre-execution: Commit quality gate ---

    [Fact]
    public async Task Pre_GitCommit_Strict_Warns()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "git commit -m 'test'");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("CommitQualityGate", verdict.Rule);
    }

    // --- Post-execution: Secret detection ---

    [Fact]
    public async Task Post_SecretInOutput_Strict_Warns()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "Found key: AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("SecretDetection", verdict.Rule);
    }

    [Fact]
    public async Task Post_SecretInInput_Strict_Warns()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolInput: "echo AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Warn, verdict.Action);
        Assert.Equal("SecretDetection", verdict.Rule);
    }

    [Fact]
    public async Task Post_CleanOutput_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "Build succeeded.");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    [Fact]
    public async Task Post_Secret_Minimal_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Minimal);
        var ctx = new GovernanceContext(GovernancePhase.Post, "Bash",
            ToolOutput: "AKIAIOSFODNN7EXAMPLE");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }

    // --- Non-shell/file tools pass through ---

    [Fact]
    public async Task Pre_NonShellTool_Allows()
    {
        var monitor = CreateMonitor(GovernanceLevel.Strict);
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Glob", ToolInput: "**/*.cs");
        var verdict = await monitor.EvaluateAsync(ctx);

        Assert.Equal(GovernanceAction.Allow, verdict.Action);
    }
}

/// <summary>No-op audit store for unit tests.</summary>
internal sealed class NullAuditStore : IAuditStore
{
    public static readonly NullAuditStore Instance = new();

    public Task LogGovernanceEventAsync(GovernanceContext context, GovernanceVerdict verdict, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task LogBashCommandAsync(string command, string? sessionId, int exitCode, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task LogPrivacyChangeAsync(string userId, string entityKind, string entityId, bool newIsPrivate, CancellationToken ct = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
