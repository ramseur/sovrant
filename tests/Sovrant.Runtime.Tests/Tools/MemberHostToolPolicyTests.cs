using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api.Types;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.Permissions;
using Sovrant.Runtime.Tools;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Tools;

/// <summary>
/// Phase 145 stopgap: on a shared Web server, members can't use tools that reach the server's disk
/// or shell unless an admin turns them on. Admins are never blocked; other tools are unaffected.
/// </summary>
public sealed class MemberHostToolPolicyTests
{
    private static JsonElement EmptyInput => JsonDocument.Parse("{}").RootElement;

    [Theory]
    [InlineData("Bash")]
    [InlineData("Read")]
    [InlineData("Write")]
    [InlineData("Grep")]
    [InlineData("PowerShell")]
    public void Members_Are_Blocked_From_Host_Tools_By_Default(string tool)
    {
        var policy = new MemberHostToolPolicy(new Principal(isAdmin: false), new Settings());
        Assert.Equal(HostToolAccess.MemberBlockedMessage, policy.GetBlockReason(tool));
    }

    [Theory]
    [InlineData("WebSearch")]
    [InlineData("Artifact")]
    [InlineData("DocumentGenerate")]
    [InlineData("mcp__github__list_issues")]
    public void Other_Tools_Are_Unaffected(string tool) =>
        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: false), new Settings()).GetBlockReason(tool));

    [Fact]
    public void Admins_Are_Never_Blocked() =>
        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: true), new Settings()).GetBlockReason("Bash"));

    [Fact]
    public async Task An_Admin_Can_Turn_Member_Access_On()
    {
        var settings = new Settings();
        var config = GovernanceConfig.Load(settings);
        Assert.False(config.MemberFileTools); // off by default
        config.MemberFileTools = true;
        await config.SaveToStoreAsync(settings);

        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: false), settings).GetBlockReason("Bash"));
    }

    [Fact]
    public async Task Executor_Refuses_Before_Running_The_Tool_Or_Asking_For_Approval()
    {
        var ran = false;
        var registry = new InMemoryToolRegistry();
        registry.Register(new ToolDefinition("Bash", EmptyInput), (_, _) => { ran = true; return Task.FromResult("ran"); });
        var executor = new DefaultToolExecutor(registry, new ModeAwarePermissionPolicy(PermissionMode.BypassPermissions),
            new NullGovernanceMonitor(), new DenyAllConfirmationHandler(), NullLogger<DefaultToolExecutor>.Instance,
            hostToolPolicy: new MemberHostToolPolicy(new Principal(isAdmin: false), new Settings()));

        var result = await executor.ExecuteAsync("Bash", EmptyInput);

        Assert.True(result.IsError);
        Assert.Equal(HostToolAccess.MemberBlockedMessage, result.Output);
        Assert.False(ran);
    }

    private sealed class Principal(bool isAdmin) : IPrincipalAccessor
    {
        public string? UserId => "sam@example.com";
        public string? Role => isAdmin ? "admin" : "user";
        public bool IsAdmin => isAdmin;
        public string? WorkspaceId => null;
    }

    private sealed class Settings : IWorkspaceSettingsStore
    {
        private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) => GetGlobalAsync(key, ct);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) { _data[key] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) { _data.Remove(key); return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_data, StringComparer.Ordinal));
    }
}
