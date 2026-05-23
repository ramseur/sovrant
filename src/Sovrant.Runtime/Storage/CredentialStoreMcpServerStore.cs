using System.Text.Json;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Storage;

/// <summary>
/// <see cref="IMcpServerStore"/> that persists every MCP server config — including
/// headers, env vars, and connection strings — as an AES-256-GCM encrypted blob in
/// <see cref="ICredentialStore"/> instead of as plaintext JSON in SQLite.
///
/// Layout in the credential store:
///   mcp.{name}.config  — full serialised <see cref="McpServerConfig"/>
///   mcp.__index__      — JSON array of known server names (for enumeration)
/// </summary>
internal sealed class CredentialStoreMcpServerStore : IMcpServerStore, IDisposable
{
    private const string IndexKey = "mcp.__index__";
    private readonly ICredentialStore _store;
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public CredentialStoreMcpServerStore(ICredentialStore store) => _store = store;

    public void Dispose() => _indexLock.Dispose();

    public async Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default)
    {
        var names = await GetIndexAsync(ct).ConfigureAwait(false);
        var result = new Dictionary<string, McpServerConfig>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var config = await GetAsync(name, ct).ConfigureAwait(false);
            if (config is not null)
                result[name] = config;
        }
        return result;
    }

    public async Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var json = await _store.RetrieveAsync(ConfigKey(name), ct).ConfigureAwait(false);
        if (json is null)
            return null;

        return JsonSerializer.Deserialize<McpServerConfig>(json, JsonOpts);
    }

    public async Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(config);

        var json = JsonSerializer.Serialize(config, JsonOpts);
        await _store.StoreAsync(ConfigKey(name), json, ct).ConfigureAwait(false);
        await UpdateIndexAsync(name, add: true, ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        await _store.DeleteAsync(ConfigKey(name), ct).ConfigureAwait(false);
        await UpdateIndexAsync(name, add: false, ct).ConfigureAwait(false);
    }

    private async Task<List<string>> GetIndexAsync(CancellationToken ct)
    {
        var json = await _store.RetrieveAsync(IndexKey, ct).ConfigureAwait(false);
        if (json is null)
            return [];
        return JsonSerializer.Deserialize<List<string>>(json, JsonOpts) ?? [];
    }

    private async Task UpdateIndexAsync(string name, bool add, CancellationToken ct)
    {
        await _indexLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var names = await GetIndexAsync(ct).ConfigureAwait(false);
            if (add)
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                    names.Add(name);
            }
            else
            {
                names.RemoveAll(n => string.Equals(n, name, StringComparison.Ordinal));
            }
            await _store.StoreAsync(IndexKey, JsonSerializer.Serialize(names, JsonOpts), ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static string ConfigKey(string name) => $"mcp.{name}.config";
}
