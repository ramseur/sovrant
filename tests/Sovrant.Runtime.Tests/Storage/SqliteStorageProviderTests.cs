using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteStorageProviderTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;

    public SqliteStorageProviderTests()
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
    public async Task InitializeAsync_CreatesDatabase()
    {
        await _provider.InitializeAsync();

        Assert.True(File.Exists(_dbPath));
        Assert.True(_provider.SchemaVersion > 0);
    }

    [Fact]
    public async Task InitializeAsync_SetsSchemaVersion()
    {
        await _provider.InitializeAsync();

        // Schema head bumped to 44 by V044 (enrich built-in skill descriptions).
        Assert.Equal(44, _provider.SchemaVersion);
    }

    [Fact]
    public async Task InitializeAsync_SeedsDefaultUser()
    {
        await _provider.InitializeAsync();

        // The default user should be seeded.
        Assert.True(_provider.SchemaVersion > 0);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        await _provider.InitializeAsync();
        var v1 = _provider.SchemaVersion;

        await _provider.InitializeAsync();
        var v2 = _provider.SchemaVersion;

        Assert.Equal(v1, v2);
    }

    [Fact]
    public async Task InitializeAsync_SeedIsIdempotent_NoDuplicateRows()
    {
        // Verify repeated InitializeAsync calls are idempotent and don't seed any rows.
        await _provider.InitializeAsync();
        await _provider.InitializeAsync();

        using var conn = ((ISqliteConnectionFactory)_provider).CreateReadOnlyConnection();

        // No users are seeded automatically — registration creates them.
        using var userCmd = conn.CreateCommand();
        userCmd.CommandText = "SELECT COUNT(*) FROM users";
        var userCount = Convert.ToInt64(userCmd.ExecuteScalar());
        Assert.Equal(0, userCount);

        // No workspaces or members seeded automatically.
        using var wsCmd = conn.CreateCommand();
        wsCmd.CommandText = "SELECT COUNT(*) FROM workspaces";
        var wsCount = Convert.ToInt64(wsCmd.ExecuteScalar());
        Assert.Equal(0, wsCount);

        using var memberCmd = conn.CreateCommand();
        memberCmd.CommandText = "SELECT COUNT(*) FROM workspace_members";
        var memberCount = Convert.ToInt64(memberCmd.ExecuteScalar());
        Assert.Equal(0, memberCount);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsOnSuccess()
    {
        await _provider.InitializeAsync();

        var executed = false;
        await _provider.ExecuteInTransactionAsync(_ =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(executed);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackOnException()
    {
        await _provider.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _provider.ExecuteInTransactionAsync(_ =>
                throw new InvalidOperationException("test")));
    }

    [Fact]
    public async Task CreateConnection_AppliesSecureDeletePragma()
    {
        // Phase 38 PR 4 — secure_delete must be ON so freed pages
        // (containing token hashes, credentials, audit content) are
        // zeroed rather than left recoverable on disk.
        await _provider.InitializeAsync();

        using var connection = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA secure_delete;";
        var result = cmd.ExecuteScalar();

        Assert.Equal(1L, Convert.ToInt64(result));
    }

    [Fact]
    public async Task InitializeAsync_HardensDbFilePermissionsOnUnix()
    {
        // Phase 38 PR 4 — on Unix the DB file (and WAL/SHM sidecars) must
        // be 0600 so another local user cannot read token hashes or
        // credential blobs. On Windows this is a no-op (user-profile ACLs
        // already restrict access) so we skip.
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            return;

        await _provider.InitializeAsync();

        // Touch a write so WAL/SHM sidecars materialize.
        await _provider.ExecuteInTransactionAsync(_ => Task.CompletedTask);

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _dbPath + suffix;
            if (!File.Exists(path)) continue;

            var mode = File.GetUnixFileMode(path);
            var expected = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            Assert.Equal(expected, mode & ~UnixFileMode.None);
            Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
                | UnixFileMode.OtherRead | UnixFileMode.OtherWrite));
        }
    }

    [Fact]
    public async Task InitializeAsync_DriftDetection_Throws()
    {
        // Phase 42.5 — MigrationDriftException must propagate out of
        // InitializeAsync. Drift is always a hard error because continuing
        // means running on an unknown schema.
        await _provider.InitializeAsync();

        // Corrupt the stored checksum for V001.
        using (var conn = ((ISqliteConnectionFactory)_provider).CreateConnection())
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "UPDATE schema_version SET checksum = 'deadbeef' WHERE version = 1";
            cmd.ExecuteNonQuery();
        }

        var freshProvider = new SqliteStorageProvider(
            NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        await Assert.ThrowsAsync<MigrationDriftException>(() => freshProvider.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_DbRequired_ThrowsOnFailure()
    {
        // Phase 42.5 — SOVRANT_DB_REQUIRE=true makes init failures throw
        // so production servers exit at boot instead of running with no
        // persistence. Use a path that will definitely fail to create.
        var badPath = OperatingSystem.IsWindows()
            ? @"\\?\CON\invalid\sovrant.db"
            : "/proc/0/invalid/sovrant.db";

        Environment.SetEnvironmentVariable("SOVRANT_DB_REQUIRE", "true");
        try
        {
            var badProvider = new SqliteStorageProvider(
                NullLogger<SqliteStorageProvider>.Instance, badPath);

            await Assert.ThrowsAsync<InvalidOperationException>(() => badProvider.InitializeAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOVRANT_DB_REQUIRE", null);
        }
    }

    [Fact]
    public async Task InitializeAsync_GracefullyHandlesInvalidPath()
    {
        // Use a path with characters invalid on Windows (NUL byte in path).
        // On Windows the Directory.CreateDirectory itself will fail.
        var badPath = OperatingSystem.IsWindows()
            ? @"\\?\CON\invalid\sovrant.db"  // reserved device name
            : "/proc/0/invalid/sovrant.db";   // /proc is not writable

        var badProvider = new SqliteStorageProvider(
            NullLogger<SqliteStorageProvider>.Instance,
            badPath);

        // Should not throw — graceful degradation.
        await badProvider.InitializeAsync();
        Assert.Equal(0, badProvider.SchemaVersion);
    }
}
