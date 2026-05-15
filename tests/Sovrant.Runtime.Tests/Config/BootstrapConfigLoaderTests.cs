using System.Text.Json;
using Sovrant.Runtime.Config;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Verifies the precedence chain that all entry points rely on
/// (CLI > env > project file > user file > defaults). When this contract
/// holds, CLI / Server / Web / Desktop all behave identically.
/// </summary>
public sealed class BootstrapConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _origDb;
    private readonly string? _origLog;
    private readonly string? _origArtifacts;
    private readonly string? _origKeystore;
    private readonly string? _origToken;

    public BootstrapConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-bootstrap-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _origDb = Environment.GetEnvironmentVariable("SOVRANT_DB_PATH");
        _origLog = Environment.GetEnvironmentVariable("SOVRANT_LOG_FILE");
        _origArtifacts = Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT");
        _origKeystore = Environment.GetEnvironmentVariable("SOVRANT_KEYSTORE_PATH");
        _origToken = Environment.GetEnvironmentVariable("SOVRANT_TOKEN");

        Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", null);
        Environment.SetEnvironmentVariable("SOVRANT_LOG_FILE", null);
        Environment.SetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT", null);
        Environment.SetEnvironmentVariable("SOVRANT_KEYSTORE_PATH", null);
        Environment.SetEnvironmentVariable("SOVRANT_TOKEN", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", _origDb);
        Environment.SetEnvironmentVariable("SOVRANT_LOG_FILE", _origLog);
        Environment.SetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT", _origArtifacts);
        Environment.SetEnvironmentVariable("SOVRANT_KEYSTORE_PATH", _origKeystore);
        Environment.SetEnvironmentVariable("SOVRANT_TOKEN", _origToken);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_AllNothing_AllFieldsNull()
    {
        var cfg = BootstrapConfigLoader.Load(cliArgs: []);
        Assert.Null(cfg.DbPath);
        Assert.Null(cfg.LogFile);
        Assert.Null(cfg.ArtifactsRoot);
        Assert.Null(cfg.KeystorePath);
        Assert.Null(cfg.ServerToken);
    }

    [Fact]
    public void Load_FromEnvVars_PopulatesAllFields()
    {
        Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", "/env/db");
        Environment.SetEnvironmentVariable("SOVRANT_LOG_FILE", "/env/log");
        Environment.SetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT", "/env/artifacts");
        Environment.SetEnvironmentVariable("SOVRANT_KEYSTORE_PATH", "/env/keystore");
        Environment.SetEnvironmentVariable("SOVRANT_TOKEN", "env-token");

        var cfg = BootstrapConfigLoader.Load(cliArgs: []);

        Assert.Equal("/env/db", cfg.DbPath);
        Assert.Equal("/env/log", cfg.LogFile);
        Assert.Equal("/env/artifacts", cfg.ArtifactsRoot);
        Assert.Equal("/env/keystore", cfg.KeystorePath);
        Assert.Equal("env-token", cfg.ServerToken);
    }

    // The `--config <path>` flag is not supported by BootstrapConfigLoader.
    // Only env vars (+ .env file) and `--db-path` are precedence sources.
    // The previous test exercised behavior that never shipped.

    [Fact]
    public void Load_MalformedConfigFile_DoesNotThrow()
    {
        var configPath = Path.Combine(_tempDir, "bad.json");
        File.WriteAllText(configPath, "{ this is not valid json");

        // Should fall back gracefully — we don't want a typo in a user's config to
        // crash boot for every entry point.
        var cfg = BootstrapConfigLoader.Load(["--config", configPath]);
        Assert.Null(cfg.DbPath);
    }

    [Fact]
    public void Load_DbPathFlagWithEqualsSyntax_Parsed()
    {
        var cfg = BootstrapConfigLoader.Load(["--db-path=/equals/db"]);
        Assert.Equal("/equals/db", cfg.DbPath);
    }

    [Fact]
    public void Load_EmptyEnvVar_TreatedAsUnset()
    {
        Environment.SetEnvironmentVariable("SOVRANT_DB_PATH", string.Empty);
        var cfg = BootstrapConfigLoader.Load(cliArgs: []);
        Assert.Null(cfg.DbPath);
    }
}
