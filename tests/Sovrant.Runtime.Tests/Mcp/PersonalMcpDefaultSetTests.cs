using Sovrant.Runtime.Config;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>
/// Phase 148 — MCP servers have a default set for personal workspaces, like models, but additive:
/// a personal workspace gets the default set plus anything enabled just for that person. Team
/// workspaces are unchanged (their own list only).
/// </summary>
public sealed class PersonalMcpDefaultSetTests
{
    private static readonly string Personal = WorkspaceIdentity.DefaultPersonalFor("sam@example.com");
    private const string Team = "ws-engineering";

    [Fact]
    public async Task A_Personal_Workspace_Gets_The_Default_Set_Plus_Its_Own()
    {
        var settings = new MemorySettings();
        await settings.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultMcpServerIds, "github");
        await settings.SetAsync(Personal, WorkspaceSettingsKeys.EnabledMcpServerIds, "postgres"); // just for Sam

        var names = (await new Servers("github", "slack", "postgres").GetEnabledEntriesAsync(settings, Personal)).Select(e => e.Name);

        Assert.Equal(["github", "postgres"], names.Order());
    }

    [Fact]
    public async Task Without_A_Default_Set_A_Personal_Workspace_Keeps_Only_Its_Own()
    {
        var settings = new MemorySettings();
        await settings.SetAsync(Personal, WorkspaceSettingsKeys.EnabledMcpServerIds, "slack");
        Assert.Equal(["slack"], (await new Servers("github", "slack").GetEnabledEntriesAsync(settings, Personal)).Select(e => e.Name));
    }

    [Fact]
    public async Task Team_Workspaces_Ignore_The_Personal_Default_Set()
    {
        var settings = new MemorySettings();
        await settings.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.PersonalDefaultMcpServerIds, "github");
        await settings.SetAsync(Team, WorkspaceSettingsKeys.EnabledMcpServerIds, "slack");
        Assert.Equal(["slack"], (await new Servers("github", "slack").GetEnabledEntriesAsync(settings, Team)).Select(e => e.Name));
    }

    private sealed class Servers(params string[] names) : IMcpServerStore
    {
        private readonly List<McpServerEntry> _entries = [.. names.Select(n => new McpServerEntry(n, n, new McpServerConfig()))];
        public Task<IReadOnlyList<McpServerEntry>> GetAllEntriesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<McpServerEntry>>(_entries);
        public Task<IReadOnlyDictionary<string, McpServerConfig>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, McpServerConfig>>(_entries.ToDictionary(e => e.Name, e => e.Config));
        public Task<McpServerConfig?> GetAsync(string name, CancellationToken ct = default) => Task.FromResult(_entries.FirstOrDefault(e => e.Name == name)?.Config);
        public Task<McpServerEntry?> GetEntryAsync(string name, CancellationToken ct = default) => Task.FromResult(_entries.FirstOrDefault(e => e.Name == name));
        public Task UpsertAsync(string name, McpServerConfig config, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MemorySettings : IWorkspaceSettingsStore
    {
        private readonly Dictionary<(string, string), string> _data = [];
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) => GetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, key, ct);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) =>
            Task.FromResult(_data.TryGetValue((workspaceId, key), out var v) ? v : null);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) { _data[(workspaceId, key)] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) { _data.Remove((workspaceId, key)); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(_data.Where(kv => kv.Key.Item1 == workspaceId).ToDictionary(kv => kv.Key.Item2, kv => kv.Value));
    }
}
