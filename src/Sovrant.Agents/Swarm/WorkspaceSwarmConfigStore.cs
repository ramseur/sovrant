using System.Globalization;
using System.Text.Json;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Agents.Swarm;

/// <summary>
/// Reads and writes <see cref="SwarmConfig"/> as individual rows in
/// <see cref="IWorkspaceSettingsStore"/> under the <c>swarm.*</c> key prefix.
/// Missing keys fall back to the <see cref="SwarmConfig"/> hardcoded defaults.
/// </summary>
public sealed class WorkspaceSwarmConfigStore(IWorkspaceSettingsStore settings) : ISwarmConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<SwarmConfig> GetAsync(string workspaceId = "", CancellationToken ct = default)
    {
        return new SwarmConfig
        {
            Enabled               = await ReadBoolAsync(workspaceId, WorkspaceSettingsKeys.SwarmEnabled,               false, ct).ConfigureAwait(false),
            MaxConcurrent         = await ReadIntAsync (workspaceId, WorkspaceSettingsKeys.SwarmMaxConcurrent,         4,     ct).ConfigureAwait(false),
            MaxTokenBudget        = await ReadIntAsync (workspaceId, WorkspaceSettingsKeys.SwarmMaxTokenBudget,        500_000, ct).ConfigureAwait(false),
            MaxRetries            = await ReadIntAsync (workspaceId, WorkspaceSettingsKeys.SwarmMaxRetries,            1,     ct).ConfigureAwait(false),
            QualityGateEnabled    = await ReadBoolAsync(workspaceId, WorkspaceSettingsKeys.SwarmQualityGate,           true,  ct).ConfigureAwait(false),
            QualityGateThreshold  = await ReadIntAsync (workspaceId, WorkspaceSettingsKeys.SwarmQualityGateThreshold,  7,     ct).ConfigureAwait(false),
            FileLocksEnabled      = await ReadBoolAsync(workspaceId, WorkspaceSettingsKeys.SwarmFileLocks,             true,  ct).ConfigureAwait(false),
            DecomposerLevel       = await ReadStringAsync(workspaceId, WorkspaceSettingsKeys.SwarmDecomposerLevel,    "High",     ct).ConfigureAwait(false),
            WorkerLevel           = await ReadStringAsync(workspaceId, WorkspaceSettingsKeys.SwarmWorkerLevel,        "Standard", ct).ConfigureAwait(false),
            TaskTimeoutSeconds    = await ReadIntAsync (workspaceId, WorkspaceSettingsKeys.SwarmTaskTimeoutSeconds,    300,   ct).ConfigureAwait(false),
            Permissions           = await ReadStringAsync(workspaceId, WorkspaceSettingsKeys.SwarmPermissions,        "ask",      ct).ConfigureAwait(false),
            TemplateOverrides     = await ReadDictAsync(workspaceId, WorkspaceSettingsKeys.SwarmTemplateOverrides,    ct).ConfigureAwait(false),
        };
    }

    public async Task SetAsync(string workspaceId, SwarmConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var ws = workspaceId;
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmEnabled,              config.Enabled               ? "true" : "false",                                    ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmMaxConcurrent,        config.MaxConcurrent.ToString(CultureInfo.InvariantCulture),                         ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmMaxTokenBudget,       config.MaxTokenBudget.ToString(CultureInfo.InvariantCulture),                        ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmMaxRetries,           config.MaxRetries.ToString(CultureInfo.InvariantCulture),                            ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmQualityGate,          config.QualityGateEnabled    ? "true" : "false",                                    ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmQualityGateThreshold, config.QualityGateThreshold.ToString(CultureInfo.InvariantCulture),                  ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmFileLocks,            config.FileLocksEnabled      ? "true" : "false",                                    ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmDecomposerLevel,      config.DecomposerLevel,                                                              ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmWorkerLevel,          config.WorkerLevel,                                                                  ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmTaskTimeoutSeconds,   config.TaskTimeoutSeconds.ToString(CultureInfo.InvariantCulture),                    ct).ConfigureAwait(false);
        await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmPermissions,          config.Permissions,                                                                  ct).ConfigureAwait(false);

        if (config.TemplateOverrides.Count > 0)
            await settings.SetAsync(ws, WorkspaceSettingsKeys.SwarmTemplateOverrides,
                JsonSerializer.Serialize(config.TemplateOverrides), ct).ConfigureAwait(false);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<bool> ReadBoolAsync(string ws, string key, bool def, CancellationToken ct)
    {
        var v = await settings.GetAsync(ws, key, ct).ConfigureAwait(false);
        if (v is null) return def;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("1", StringComparison.Ordinal);
    }

    private async Task<int> ReadIntAsync(string ws, string key, int def, CancellationToken ct)
    {
        var v = await settings.GetAsync(ws, key, ct).ConfigureAwait(false);
        return v is not null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : def;
    }

    private async Task<string> ReadStringAsync(string ws, string key, string def, CancellationToken ct)
    {
        var v = await settings.GetAsync(ws, key, ct).ConfigureAwait(false);
        return string.IsNullOrEmpty(v) ? def : v;
    }

    private async Task<Dictionary<string, string>> ReadDictAsync(string ws, string key, CancellationToken ct)
    {
        var v = await settings.GetAsync(ws, key, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(v)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(v, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
}
