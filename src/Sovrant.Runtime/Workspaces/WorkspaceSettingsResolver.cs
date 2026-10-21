using System.Globalization;
using System.Text.Json;

namespace Sovrant.Runtime.Workspaces;

/// <summary>
/// Shared resolver for global settings that also have an environment variable (budgets, session caps,
/// executor limits, agent concurrency, compaction, governance, trust boundary). Phase 148: the stored
/// value wins and the variable is the fallback — it seeds the database at startup
/// (<see cref="Config.EnvBackedSettings.SeedAsync"/>) — unless <c>SOVRANT_ENV_OVERRIDE</c> is on, when the
/// variable wins and the app shows the setting locked. The fallback covers a fresh install or an
/// unavailable store.
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
    // Phase 148: the stored value wins and the variable is only a fallback (it seeds the database at
    // startup) — unless SOVRANT_ENV_OVERRIDE is on, when the variable wins (and the app shows it locked).
    private static IEnumerable<string?> Candidates(IWorkspaceSettingsStore? settings, string key, string envVar, Func<string, string?>? env)
    {
        env ??= Environment.GetEnvironmentVariable;
        var fromEnv = env(envVar);
        if (Config.EnvOverride.IsOn(env))
        {
            yield return fromEnv;
            if (settings is not null) yield return ReadGlobal(settings, key);
        }
        else
        {
            if (settings is not null) yield return ReadGlobal(settings, key);
            yield return fromEnv;
        }
    }

    /// <summary>Resolves an integer setting: stored &gt; env &gt; fallback (env first with the override on).</summary>
    public static int ResolveInt(IWorkspaceSettingsStore? settings, string key, string envVar, int fallback, Func<string, string?>? env = null)
    {
        foreach (var raw in Candidates(settings, key, envVar, env))
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;
        return fallback;
    }

    /// <summary>Resolves a decimal setting the same way. Returns null when nothing parses.</summary>
    public static decimal? ResolveDecimalOrNull(IWorkspaceSettingsStore? settings, string key, string envVar, Func<string, string?>? env = null)
    {
        foreach (var raw in Candidates(settings, key, envVar, env))
            if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                return v;
        return null;
    }

    /// <summary>Resolves a string setting the same way.</summary>
    public static string? ResolveString(IWorkspaceSettingsStore? settings, string key, string envVar, string? fallback, Func<string, string?>? env = null)
    {
        foreach (var raw in Candidates(settings, key, envVar, env))
            if (!string.IsNullOrEmpty(raw))
                return raw;
        return fallback;
    }

    /// <summary>Resolves a boolean setting the same way.</summary>
    public static bool ResolveBool(IWorkspaceSettingsStore? settings, string key, string envVar, bool fallback, Func<string, string?>? env = null)
    {
        foreach (var raw in Candidates(settings, key, envVar, env))
            if (TryParseBool(raw, out var v))
                return v;
        return fallback;
    }

    /// <summary>Resolves a list setting (JSON array or comma-separated) the same way.</summary>
    public static IReadOnlyList<string> ResolveStringList(
        IWorkspaceSettingsStore? settings, string key, string envVar, IReadOnlyList<string> fallback, Func<string, string?>? env = null)
    {
        foreach (var raw in Candidates(settings, key, envVar, env))
            if (!string.IsNullOrWhiteSpace(raw) && TryParseStringList(raw, out var list))
                return list;
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
