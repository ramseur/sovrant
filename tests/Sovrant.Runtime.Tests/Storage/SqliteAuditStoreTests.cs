using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteAuditStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly IAuditStore _store;

    public SqliteAuditStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteAuditStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task LogGovernanceEvent_DoesNotThrow()
    {
        var ctx = new GovernanceContext(GovernancePhase.Pre, "Bash", ToolInput: "rm -rf /", SessionId: "s1");
        var verdict = new GovernanceVerdict(GovernanceAction.Block, "Dangerous", "DangerousCommand");

        await _store.LogGovernanceEventAsync(ctx, verdict);
    }

    [Fact]
    public async Task LogBashCommand_DoesNotThrow()
    {
        await _store.LogBashCommandAsync("ls -la", "s1", 0);
    }

    [Fact]
    public async Task LogMultipleEvents_DoesNotThrow()
    {
        for (var i = 0; i < 10; i++)
        {
            await _store.LogBashCommandAsync($"cmd{i}", $"s{i}", i);
        }
    }
}
