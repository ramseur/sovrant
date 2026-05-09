namespace Sovrant.Runtime.Config;

/// <summary>
/// Loads <see cref="BootstrapConfig"/> from environment variables, with an
/// optional <c>.env</c> file providing defaults. Precedence (highest wins):
/// <list type="number">
///   <item>CLI flag: <c>--db-path &lt;path&gt;</c></item>
///   <item>Process environment variables</item>
///   <item><c>.env</c> file in the current working directory</item>
///   <item>Built-in defaults (resolved lazily by each consumer)</item>
/// </list>
/// <para>
/// The <c>.env</c> file uses standard <c>KEY=VALUE</c> syntax. Lines starting
/// with <c>#</c> are comments; blank lines are ignored; quoted values have
/// their surrounding quotes stripped. Variables already present in the process
/// environment are never overwritten by the <c>.env</c> file.
/// </para>
/// </summary>
public static class BootstrapConfigLoader
{
    /// <summary>
    /// Resolves the active <see cref="BootstrapConfig"/> by merging all layers.
    /// Pass the process arguments (typically <c>args</c> from <c>Main</c>) to
    /// honour the <c>--db-path</c> CLI override; pass <c>null</c> to skip it.
    /// </summary>
    public static BootstrapConfig Load(string[]? cliArgs = null)
    {
        LoadDotEnvFile();

        var env = ReadEnvironment();
        var dbPathOverride = ParseDbPathArg(cliArgs);

        return new BootstrapConfig
        {
            DbPath          = dbPathOverride ?? env.DbPath,
            LogFile         = env.LogFile,
            ArtifactsRoot   = env.ArtifactsRoot,
            KeystorePath    = env.KeystorePath,
            ServerToken     = env.ServerToken,
            TlsCertPath     = env.TlsCertPath,
            TlsCertPassword = env.TlsCertPassword,
            TlsKeyPath      = env.TlsKeyPath,
            TlsHttpsPort    = env.TlsHttpsPort,
        };
    }

    // ── .env file ────────────────────────────────────────────────────────────

    private static void LoadDotEnvFile()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path))
            return;

        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                var eq = line.IndexOf('=', StringComparison.Ordinal);
                if (eq <= 0)
                    continue;

                var key   = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();

                if (string.IsNullOrEmpty(key))
                    continue;

                // Strip surrounding quotes.
                if (value.Length >= 2 &&
                    ((value[0] == '"'  && value[^1] == '"') ||
                     (value[0] == '\'' && value[^1] == '\'')))
                    value = value[1..^1];

                // Never overwrite values already in the process environment.
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[bootstrap] Could not read .env: {ex.Message}. Continuing without it.");
        }
    }

    // ── Environment variables ─────────────────────────────────────────────────

    private static BootstrapConfig ReadEnvironment() => new()
    {
        DbPath          = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_DB_PATH")),
        LogFile         = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_LOG_FILE")),
        ArtifactsRoot   = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_ARTIFACTS_ROOT")),
        KeystorePath    = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_KEYSTORE_PATH")),
        ServerToken     = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TOKEN")),
        TlsCertPath     = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TLS_CERT")),
        TlsCertPassword = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TLS_CERT_PASSWORD")),
        TlsKeyPath      = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TLS_KEY")),
        TlsHttpsPort    = NullIfEmpty(Environment.GetEnvironmentVariable("SOVRANT_TLS_HTTPS_PORT")),
    };

    // ── CLI ───────────────────────────────────────────────────────────────────

    private static string? ParseDbPathArg(string[]? args)
    {
        if (args is null)
            return null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--db-path", StringComparison.Ordinal) && i + 1 < args.Length)
                return args[++i];
            if (arg.StartsWith("--db-path=", StringComparison.Ordinal))
                return arg["--db-path=".Length..];
        }

        return null;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;
}
