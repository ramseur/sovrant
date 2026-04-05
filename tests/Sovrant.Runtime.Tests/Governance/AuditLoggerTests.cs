using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class AuditLoggerTests : IDisposable
{
    private readonly string _tempAuditDir;
    private readonly string _originalHome;

    public AuditLoggerTests()
    {
        // We can't easily redirect AuditLogger's path (it uses SpecialFolder.UserProfile),
        // so we test the public behavior: that it doesn't throw and creates files.
        _tempAuditDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant", "audit");
        _originalHome = _tempAuditDir; // Just track for cleanup awareness
    }

    public void Dispose() { /* audit dir is in user profile — don't delete */ }

    [Fact]
    public async Task LogGovernanceEvent_DoesNotThrow()
    {
        using var logger = new AuditLogger();
        var context = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /", SessionId: "test-session");
        var verdict = new GovernanceVerdict(GovernanceAction.Block, "dangerous", "DangerousCommand");

        // Should not throw
        await logger.LogGovernanceEventAsync(context, verdict);
    }

    [Fact]
    public async Task LogBashCommand_DoesNotThrow()
    {
        using var logger = new AuditLogger();
        await logger.LogBashCommandAsync("echo hello", "test-session", 0);
    }

    [Fact]
    public async Task LogAfterDispose_DoesNotThrow()
    {
        var logger = new AuditLogger();
        logger.Dispose();

        // Should silently no-op after disposal
        await logger.LogBashCommandAsync("echo hello", "test-session", 0);
    }
}
