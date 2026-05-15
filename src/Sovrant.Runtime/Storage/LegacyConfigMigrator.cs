using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Preferences;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// Phase 88-F — one-shot migrator that ingests the three legacy JSON
/// config files into their new DB / credential-store homes:
/// <list type="bullet">
///   <item><c>~/.sovrant/settings.json</c> → <see cref="IUserPreferenceStore"/>
///         (per-key) plus the <c>ApiKey</c> field into <see cref="ICredentialStore"/></item>
///   <item><c>~/.sovrant/providers.json</c> → <see cref="IProviderProfileStore"/>
///         (one row per profile) plus each profile's <c>ApiKey</c> into
///         <see cref="ICredentialStore"/> under a profile-derived id</item>
///   <item><c>~/.sovrant/governance.json</c> → <see cref="IWorkspaceSettingsStore"/>
///         (global row) for the five governance fields</item>
/// </list>
/// After successful ingest each source file is renamed to <c>*.json.bak</c>
/// so the user retains an offline copy and the migrator becomes idempotent
/// (a missing original is a no-op on subsequent boots).
/// </summary>
public sealed partial class LegacyConfigMigrator
{
    private readonly IUserPreferenceStore _preferences;
    private readonly IProviderProfileStore _profiles;
    private readonly ICredentialStore _credentials;
    private readonly IWorkspaceSettingsStore _workspaceSettings;
    private readonly ILogger<LegacyConfigMigrator> _logger;
    private readonly string _baseDir;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrating legacy config file {Path} → DB ({Fields} fields, {Secrets} secrets ingested)")]
    private static partial void LogMigrating(ILogger logger, string path, int fields, int secrets);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renamed {Source} → {Backup}")]
    private static partial void LogRenamed(ILogger logger, string source, string backup);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to migrate {Path}: {Error}. Source file left in place; the import will be retried on next boot.")]
    private static partial void LogFailure(ILogger logger, string path, string error);

    public LegacyConfigMigrator(
        IUserPreferenceStore preferences,
        IProviderProfileStore profiles,
        ICredentialStore credentials,
        IWorkspaceSettingsStore workspaceSettings,
        ILogger<LegacyConfigMigrator> logger,
        string? baseDirectory = null)
    {
        _preferences = preferences;
        _profiles = profiles;
        _credentials = credentials;
        _workspaceSettings = workspaceSettings;
        _logger = logger;
        _baseDir = baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".sovrant");
    }

    /// <summary>
    /// Runs the migrator. Each file is independent — failure to read one
    /// does not skip the others. Returns the number of source files
    /// successfully migrated this run (0 on a clean install or after the
    /// first run has already converted everything).
    /// </summary>
    public async Task<int> RunAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var migrated = 0;
        if (await TryMigrateSettingsAsync(userId, ct).ConfigureAwait(false)) migrated++;
        if (await TryMigrateProvidersAsync(userId, ct).ConfigureAwait(false)) migrated++;
        if (await TryMigrateGovernanceAsync(ct).ConfigureAwait(false)) migrated++;
        if (await TryMigrateSwarmConfigAsync(ct).ConfigureAwait(false)) migrated++;
        return migrated;
    }

    // ── settings.json ───────────────────────────────────────────────────

    private async Task<bool> TryMigrateSettingsAsync(string userId, CancellationToken ct)
    {
        var path = Path.Combine(_baseDir, "settings.json");
        if (!File.Exists(path)) return false;
        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
            var root = doc.RootElement;

            var fields = 0;
            var secrets = 0;

            fields += await CopyStringPrefAsync(root, "Model", userId, UserPreferenceKeys.Model, ct).ConfigureAwait(false);
            fields += await CopyStringPrefAsync(root, "Provider", userId, UserPreferenceKeys.Provider, ct).ConfigureAwait(false);
            fields += await CopyStringPrefAsync(root, "BaseUrl", userId, UserPreferenceKeys.BaseUrl, ct).ConfigureAwait(false);
            fields += await CopyStringPrefAsync(root, "PermissionMode", userId, UserPreferenceKeys.PermissionMode, ct).ConfigureAwait(false);
            fields += await CopyStringPrefAsync(root, "WebSearch", userId, UserPreferenceKeys.WebSearch, ct).ConfigureAwait(false);
            fields += await CopyIntPrefAsync(root, "MaxTokens", userId, UserPreferenceKeys.MaxTokens, ct).ConfigureAwait(false);
            fields += await CopyBoolPrefAsync(root, "IntentRouting", userId, UserPreferenceKeys.IntentRouting, ct).ConfigureAwait(false);

            if (TryReadString(root, "ApiKey") is { Length: > 0 } apiKey)
            {
                await _credentials.StoreAsync(Sovrant.Api.Auth.CredentialKeys.LlmApiKey, apiKey, ct).ConfigureAwait(false);
                secrets++;
            }

            LogMigrating(_logger, path, fields, secrets);
            RenameToBak(path);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogFailure(_logger, path, ex.Message);
            return false;
        }
    }

    // ── providers.json ──────────────────────────────────────────────────

    private async Task<bool> TryMigrateProvidersAsync(string userId, CancellationToken ct)
    {
        var path = Path.Combine(_baseDir, "providers.json");
        if (!File.Exists(path)) return false;
        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                LogFailure(_logger, path, "root element is not an array");
                return false;
            }

            var fields = 0;
            var secrets = 0;
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                var name = TryReadString(entry, "Name") ?? "Unnamed";
                var provider = TryReadString(entry, "Provider") ?? "OpenAI";
                var model = TryReadString(entry, "Model");
                var baseUrl = TryReadString(entry, "BaseUrl") ?? string.Empty;
                var apiKey = TryReadString(entry, "ApiKey");
                var maxTokens = TryReadInt(entry, "MaxTokens");

                var profileId = MakeProfileId(provider, name);
                var credentialId = $"provider.{profileId}.api_key";

                if (!string.IsNullOrEmpty(apiKey))
                {
                    await _credentials.StoreAsync(credentialId, apiKey, ct).ConfigureAwait(false);
                    secrets++;
                }

                var profile = new ProviderProfile(
                    ProfileId: profileId,
                    UserId: userId,
                    Name: name,
                    ProviderKind: provider,
                    BaseUrl: baseUrl,
                    DefaultModel: model,
                    MaxTokens: maxTokens,
                    CredentialId: credentialId,
                    CreatedAt: now,
                    UpdatedAt: now);

                // Idempotency at the row level — if a previous partial run
                // already inserted this row, swap update for create.
                var existing = await _profiles.GetAsync(profileId, ct).ConfigureAwait(false);
                if (existing is null)
                    await _profiles.CreateAsync(profile, ct).ConfigureAwait(false);
                else
                    await _profiles.UpdateAsync(profile, ct).ConfigureAwait(false);
                fields++;
            }

            LogMigrating(_logger, path, fields, secrets);
            RenameToBak(path);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogFailure(_logger, path, ex.Message);
            return false;
        }
    }

    // ── governance.json ─────────────────────────────────────────────────

    private async Task<bool> TryMigrateGovernanceAsync(CancellationToken ct)
    {
        var path = Path.Combine(_baseDir, "governance.json");
        if (!File.Exists(path)) return false;
        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
            var root = doc.RootElement;
            var ws = WorkspaceSettingsKeys.GlobalWorkspaceId;

            var fields = 0;

            if (TryReadString(root, "governance_level") is { Length: > 0 } level)
            {
                if (await _workspaceSettings.GetAsync(ws, WorkspaceSettingsKeys.GovernanceLevel, ct).ConfigureAwait(false) is null)
                {
                    await _workspaceSettings.SetAsync(ws, WorkspaceSettingsKeys.GovernanceLevel, level, ct).ConfigureAwait(false);
                    fields++;
                }
            }

            if (root.TryGetProperty("audit_log", out var auditEl) && auditEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                if (await _workspaceSettings.GetAsync(ws, WorkspaceSettingsKeys.GovernanceAuditLog, ct).ConfigureAwait(false) is null)
                {
                    await _workspaceSettings.SetAsync(ws, WorkspaceSettingsKeys.GovernanceAuditLog,
                        auditEl.GetBoolean() ? "true" : "false", ct).ConfigureAwait(false);
                    fields++;
                }
            }

            fields += await CopyJsonArrayAsync(root, "blocked_commands", WorkspaceSettingsKeys.GovernanceBlockedCommands, ct).ConfigureAwait(false);
            fields += await CopyJsonArrayAsync(root, "protected_files", WorkspaceSettingsKeys.GovernanceProtectedFiles, ct).ConfigureAwait(false);
            fields += await CopyJsonArrayAsync(root, "secret_patterns", WorkspaceSettingsKeys.GovernanceSecretPatterns, ct).ConfigureAwait(false);

            LogMigrating(_logger, path, fields, 0);
            RenameToBak(path);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogFailure(_logger, path, ex.Message);
            return false;
        }
    }

    // ── swarm.json ──────────────────────────────────────────────────────

    private async Task<bool> TryMigrateSwarmConfigAsync(CancellationToken ct)
    {
        // swarm.json lives next to the workspace (CWD/.sovrant/), not in _baseDir.
        var path = Path.Combine(Directory.GetCurrentDirectory(), ".sovrant", "swarm.json");
        if (!File.Exists(path)) return false;
        try
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct).ConfigureAwait(false));
            var root = doc.RootElement;
            var ws = WorkspaceSettingsKeys.GlobalWorkspaceId;
            var fields = 0;

            fields += await MigrateSwarmBoolAsync(root, "enabled",                ws, WorkspaceSettingsKeys.SwarmEnabled,               ct).ConfigureAwait(false);
            fields += await MigrateSwarmIntAsync (root, "max_concurrent",          ws, WorkspaceSettingsKeys.SwarmMaxConcurrent,          ct).ConfigureAwait(false);
            fields += await MigrateSwarmIntAsync (root, "max_token_budget",        ws, WorkspaceSettingsKeys.SwarmMaxTokenBudget,         ct).ConfigureAwait(false);
            fields += await MigrateSwarmIntAsync (root, "max_retries",             ws, WorkspaceSettingsKeys.SwarmMaxRetries,             ct).ConfigureAwait(false);
            fields += await MigrateSwarmBoolAsync(root, "quality_gate",            ws, WorkspaceSettingsKeys.SwarmQualityGate,            ct).ConfigureAwait(false);
            fields += await MigrateSwarmIntAsync (root, "quality_gate_threshold",  ws, WorkspaceSettingsKeys.SwarmQualityGateThreshold,   ct).ConfigureAwait(false);
            fields += await MigrateSwarmBoolAsync(root, "file_locks_enabled",      ws, WorkspaceSettingsKeys.SwarmFileLocks,              ct).ConfigureAwait(false);
            fields += await MigrateSwarmStringAsync(root, "decomposer_level",      ws, WorkspaceSettingsKeys.SwarmDecomposerLevel,        ct).ConfigureAwait(false);
            fields += await MigrateSwarmStringAsync(root, "worker_level",          ws, WorkspaceSettingsKeys.SwarmWorkerLevel,            ct).ConfigureAwait(false);
            fields += await MigrateSwarmIntAsync (root, "task_timeout_seconds",    ws, WorkspaceSettingsKeys.SwarmTaskTimeoutSeconds,     ct).ConfigureAwait(false);
            fields += await MigrateSwarmStringAsync(root, "permissions",           ws, WorkspaceSettingsKeys.SwarmPermissions,            ct).ConfigureAwait(false);
            fields += await CopyJsonObjectAsync(root, "templates",                 ws, WorkspaceSettingsKeys.SwarmTemplateOverrides,      ct).ConfigureAwait(false);

            LogMigrating(_logger, path, fields, 0);
            RenameToBak(path);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogFailure(_logger, path, ex.Message);
            return false;
        }
    }

    private async Task<int> MigrateSwarmBoolAsync(JsonElement root, string source, string ws, string key, CancellationToken ct)
    {
        if (!root.TryGetProperty(source, out var el) || el.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return 0;
        if (await _workspaceSettings.GetAsync(ws, key, ct).ConfigureAwait(false) is not null) return 0;
        await _workspaceSettings.SetAsync(ws, key, el.GetBoolean() ? "true" : "false", ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> MigrateSwarmIntAsync(JsonElement root, string source, string ws, string key, CancellationToken ct)
    {
        if (!root.TryGetProperty(source, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v)) return 0;
        if (await _workspaceSettings.GetAsync(ws, key, ct).ConfigureAwait(false) is not null) return 0;
        await _workspaceSettings.SetAsync(ws, key, v.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> MigrateSwarmStringAsync(JsonElement root, string source, string ws, string key, CancellationToken ct)
    {
        if (TryReadString(root, source) is not { Length: > 0 } v) return 0;
        if (await _workspaceSettings.GetAsync(ws, key, ct).ConfigureAwait(false) is not null) return 0;
        await _workspaceSettings.SetAsync(ws, key, v, ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> CopyJsonObjectAsync(JsonElement root, string source, string ws, string targetKey, CancellationToken ct)
    {
        if (!root.TryGetProperty(source, out var el) || el.ValueKind != JsonValueKind.Object) return 0;
        if (await _workspaceSettings.GetAsync(ws, targetKey, ct).ConfigureAwait(false) is not null) return 0;
        await _workspaceSettings.SetAsync(ws, targetKey, el.GetRawText(), ct).ConfigureAwait(false);
        return 1;
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task<int> CopyStringPrefAsync(JsonElement root, string source, string userId, string key, CancellationToken ct)
    {
        if (TryReadString(root, source) is not { Length: > 0 } v) return 0;
        if (await _preferences.GetAsync(userId, key, ct).ConfigureAwait(false) is not null) return 0;
        await _preferences.SetAsync(userId, key, v, ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> CopyIntPrefAsync(JsonElement root, string source, string userId, string key, CancellationToken ct)
    {
        if (TryReadInt(root, source) is not { } v) return 0;
        if (await _preferences.GetAsync(userId, key, ct).ConfigureAwait(false) is not null) return 0;
        await _preferences.SetAsync(userId, key, v.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> CopyBoolPrefAsync(JsonElement root, string source, string userId, string key, CancellationToken ct)
    {
        if (!root.TryGetProperty(source, out var el)) return 0;
        if (el.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return 0;
        if (await _preferences.GetAsync(userId, key, ct).ConfigureAwait(false) is not null) return 0;
        await _preferences.SetAsync(userId, key, el.GetBoolean() ? "true" : "false", ct).ConfigureAwait(false);
        return 1;
    }

    private async Task<int> CopyJsonArrayAsync(JsonElement root, string source, string targetKey, CancellationToken ct)
    {
        if (!root.TryGetProperty(source, out var el) || el.ValueKind != JsonValueKind.Array) return 0;
        if (await _workspaceSettings.GetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, targetKey, ct).ConfigureAwait(false) is not null) return 0;
        await _workspaceSettings.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, targetKey, el.GetRawText(), ct).ConfigureAwait(false);
        return 1;
    }

    private static string? TryReadString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static int? TryReadInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el)) return null;
        return el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v) ? v : null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Profile ids are URL-style slugs and conventionally lowercase; never compared as security tokens.")]
    private static string MakeProfileId(string provider, string name)
    {
        var slug = $"{provider}-{name}".ToLowerInvariant();
        var sanitized = new string(slug.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return sanitized.Trim('-');
    }

    private void RenameToBak(string source)
    {
        var backup = source + ".bak";
        try
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(source, backup);
            LogRenamed(_logger, source, backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFailure(_logger, source, $"could not rename to {backup}: {ex.Message}");
        }
    }
}
