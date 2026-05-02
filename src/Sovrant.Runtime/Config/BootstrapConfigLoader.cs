using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sovrant.Runtime.Config;

/// <summary>
/// Loads <see cref="BootstrapConfig"/> from a layered set of sources, in
/// decreasing order of precedence:
/// <list type="number">
///   <item>CLI flags that set a single value (<c>--db-path</c>)</item>
///   <item>Environment variables (<c>SOVRANT_DB_PATH</c>, <c>SOVRANT_LOG_FILE</c>,
///     <c>SOVRANT_ARTIFACTS_ROOT</c>, <c>SOVRANT_KEYSTORE_PATH</c>, <c>SOVRANT_TOKEN</c>)</item>
///   <item><c>--config &lt;path&gt;</c> file (a user-pointed JSON file)</item>
///   <item>Project file: <c>./.sovrant/sovrant.config.json</c></item>
///   <item>User file: <c>~/.sovrant/sovrant.config.json</c></item>
///   <item>Built-in defaults (resolved lazily by each consumer)</item>
/// </list>
/// </summary>
public static class BootstrapConfigLoader
{
    private const string ProjectConfigPath = ".sovrant/sovrant.config.json";
    private const string UserConfigFileName = "sovrant.config.json";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Resolves the active <see cref="BootstrapConfig"/> by merging all layers.
    /// Pass the process arguments (typically <c>args</c> from <c>Main</c>) to
    /// honour CLI overrides; pass <c>null</c> to skip the CLI layer.
    /// </summary>
    public static BootstrapConfig Load(string[]? cliArgs = null)
    {
        var userFile = LoadFile(UserFilePath());
        var projectFile = LoadFile(ProjectFilePath());
        var env = LoadEnvironment();
        var (cli, configFile) = ParseCli(cliArgs);

        // Merge layers (highest-precedence wins via FirstNonNull):
        // CLI > env > --config file > project file > user file > defaults.
        return new BootstrapConfig
        {
            DbPath = FirstNonNull(cli.DbPath, env.DbPath, configFile.DbPath, projectFile.DbPath, userFile.DbPath),
            LogFile = FirstNonNull(cli.LogFile, env.LogFile, configFile.LogFile, projectFile.LogFile, userFile.LogFile),
            ArtifactsRoot = FirstNonNull(cli.ArtifactsRoot, env.ArtifactsRoot, configFile.ArtifactsRoot, projectFile.ArtifactsRoot, userFile.ArtifactsRoot),
            KeystorePath = FirstNonNull(cli.KeystorePath, env.KeystorePath, configFile.KeystorePath, projectFile.KeystorePath, userFile.KeystorePath),
            ServerToken = FirstNonNull(cli.ServerToken, env.ServerToken, configFile.ServerToken, projectFile.ServerToken, userFile.ServerToken),
        };
    }

    /// <summary>The resolved path to the user-level config file (not guaranteed to exist).</summary>
    public static string UserFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sovrant", UserConfigFileName);

    /// <summary>The resolved path to the project-level config file (not guaranteed to exist).</summary>
    public static string ProjectFilePath() => Path.Combine(
        Directory.GetCurrentDirectory(), ProjectConfigPath);

    private static BootstrapConfig LoadFile(string path)
    {
        if (!File.Exists(path))
            return new BootstrapConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<BootstrapConfig>(json, s_jsonOptions) ?? new BootstrapConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Tolerant of malformed user config — fall back to defaults rather than crashing boot.
            Console.Error.WriteLine($"[bootstrap] Failed to read {path}: {ex.Message}. Ignoring file.");
            return new BootstrapConfig();
        }
    }

    private static BootstrapConfig LoadEnvironment() => new()
    {
        DbPath = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_DB_PATH")),
        LogFile = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_LOG_FILE")),
        ArtifactsRoot = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")),
        KeystorePath = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_KEYSTORE_PATH")),
        ServerToken = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TOKEN")),
    };

    private static (BootstrapConfig Cli, BootstrapConfig ConfigFile) ParseCli(string[]? args)
    {
        if (args is null || args.Length == 0)
            return (new BootstrapConfig(), new BootstrapConfig());

        string? dbPath = null;
        string? configPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--db-path", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                dbPath = args[++i];
            }
            else if (arg.StartsWith("--db-path=", StringComparison.Ordinal))
            {
                dbPath = arg["--db-path=".Length..];
            }
            else if (string.Equals(arg, "--config", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                configPath = args[++i];
            }
            else if (arg.StartsWith("--config=", StringComparison.Ordinal))
            {
                configPath = arg["--config=".Length..];
            }
        }

        // CLI flags only carry literal value overrides. The --config file is
        // returned as its own tier so the caller can slot it between env and
        // the standard project/user files.
        var cli = new BootstrapConfig { DbPath = dbPath };
        var configFile = string.IsNullOrEmpty(configPath) ? new BootstrapConfig() : LoadFile(configPath!);
        return (cli, configFile);
    }

    private static string? FirstNonNull(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c))
                return c;
        }
        return null;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
