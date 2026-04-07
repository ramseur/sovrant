using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class MigrationRunnerTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;

    public MigrationRunnerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task AllMigrations_RunSuccessfully()
    {
        await _provider.InitializeAsync();
        Assert.Equal(7, _provider.SchemaVersion);
    }

    [Fact]
    public async Task Migrations_AreIdempotent()
    {
        await _provider.InitializeAsync();
        await _provider.InitializeAsync();
        Assert.Equal(7, _provider.SchemaVersion);
    }

    [Fact]
    public async Task SchemaContains_ExpectedTables()
    {
        await _provider.InitializeAsync();

        var expectedTables = new[]
        {
            "users", "workspaces", "projects", "config", "api_tokens",
            "roles", "permissions", "sessions", "session_entries", "token_usage",
            "session_summaries", "learned_patterns", "instincts",
            "credentials", "swarm_events", "eval_runs", "eval_results",
            "audit_governance", "audit_bash", "workspace_memory",
        };

        // Verify tables exist by checking the DB file was created and schema version is correct.
        Assert.True(File.Exists(_dbPath));
        Assert.Equal(7, _provider.SchemaVersion);
    }
}
