using System.Globalization;
using System.Text.Json;

namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Shared env &gt; <see cref="IWorkspaceSettingsStore"/> &gt; hardcoded-fallback
/// resolver used by Bucket-B consumers (budgets, session caps, executor limits,
/// agent concurrency, compaction threshold). Centralised so the precedence
/// chain stays consistent — env vars always win for 12-factor parity, the DB
/// holds the persistent per-workspace value, and the fallback covers the
/// fresh-install / store-unavailable case.
/// </summary>
/// <remarks>
/// Resolution is synchronous-over-async by design: every consumer reads at DI
/// construction time and caches the result for the process lifetime, matching
/// the pre-existing <see cref="Metrics.BudgetEnforcer"/> pattern. Hot-swap
/// from the Settings UI is a separate concern handled by mutable singletons
/// (e.g. <c>RouterOptions</c>) where it's actually needed.
/// </remarks>
public static class WorkspaceSettingsResolver
{
    /// <summary>Resolves an integer setting via env &gt; store &gt; fallback.</summary>
    public static int ResolveInt(IWorkspaceSettingsStore? settings, string key, string envVar, int fallback)
    {
        if (int.TryParse(Environment.GetEnvironmentVariable(envVar),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromEnv))
            return fromEnv;

        if (settings is not null)
        {
            var fromDb = ReadGlobal(settings, key);
            if (int.TryParse(fromDb, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return fallback;
    }

    /// <summary>Resolves a decimal setting via env &gt; store &gt; fallback. Returns null when nothing parses.</summary>
    public static decimal? ResolveDecimalOrNull(IWorkspaceSettingsStore? settings, string key, string envVar)
    {
        if (decimal.TryParse(Environment.GetEnvironmentVariable(envVar),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var fromEnv))
            return fromEnv;

        if (settings is not null)
        {
            var fromDb = ReadGlobal(settings, key);
            if (decimal.TryParse(fromDb, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }

    /// <summary>Resolves a string setting via env &gt; store &gt; fallback.</summary>
    public static string? ResolveString(IWorkspaceSettingsStore? settings, string key, string envVar, string? fallback)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;

        if (settings is not null)
        {
            var fromDb = ReadGlobal(settings, key);
            if (!string.IsNullOrEmpty(fromDb)) return fromDb;
        }

        return fallback;
    }

    /// <summary>
    /// Resolves a boolean setting via env &gt; store &gt; fallback. Accepts
    /// <c>true</c>/<c>false</c>, <c>1</c>/<c>0</c>, <c>yes</c>/<c>no</c>,
    /// <c>on</c>/<c>off</c> (case-insensitive). Unparseable values fall through.
    /// </summary>
    public static bool ResolveBool(IWorkspaceSettingsStore? settings, string key, string envVar, bool fallback)
    {
        if (TryParseBool(Environment.GetEnvironmentVariable(envVar), out var fromEnv))
            return fromEnv;

        if (settings is not null)
        {
            var fromDb = ReadGlobal(settings, key);
            if (TryParseBool(fromDb, out var parsed))
                return parsed;
        }

        return fallback;
    }

    /// <summary>
    /// Resolves a string-list setting via env &gt; store &gt; fallback. The store value
    /// is a JSON array (e.g. <c>["a","b"]</c>); the env value is either a JSON array
    /// or a comma-separated list (whitespace-trimmed).
    /// </summary>
    public static IReadOnlyList<string> ResolveStringList(
        IWorkspaceSettingsStore? settings, string key, string envVar, IReadOnlyList<string> fallback)
    {
        var fromEnv = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(fromEnv) && TryParseStringList(fromEnv, out var envList))
            return envList;

        if (settings is not null)
        {
            var fromDb = ReadGlobal(settings, key);
            if (!string.IsNullOrWhiteSpace(fromDb) && TryParseStringList(fromDb, out var dbList))
                return dbList;
        }

        return fallback;
    }

    private static bool TryParseBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        switch (raw.Trim().ToUpperInvariant())
        {
            case "TRUE": case "1": case "YES": case "ON":
                value = true; return true;
            case "FALSE": case "0": case "NO": case "OFF":
                value = false; return true;
            default:
                return false;
        }
    }

    private static bool TryParseStringList(string raw, out IReadOnlyList<string> list)
    {
        list = Array.Empty<string>();
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return false;

        // JSON array form
        if (trimmed[0] == '[')
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(trimmed);
                if (parsed is not null)
                {
                    list = parsed;
                    return true;
                }
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        // Comma-separated form (env-friendly).
        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;
        list = parts;
        return true;
    }

    private static string? ReadGlobal(IWorkspaceSettingsStore settings, string key)
    {
        try
        {
            return settings.GetGlobalAsync(key).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or System.Data.Common.DbException
            or IOException
            or UnauthorizedAccessException)
        {
            // Store unavailable (e.g. DB not yet migrated) — fall through.
            return null;
        }
    }
}
