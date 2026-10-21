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
/// Phase 145 Part D: on a hosted server (Web, Server), tools that reach the server's disk or shell are
/// off for everyone — admins included — until an admin allows them; members then also need the member
/// switch. Blocked tools are hidden from the model. Other tools are unaffected.
/// </summary>
public sealed class MemberHostToolPolicyTests
{
    private static JsonElement EmptyInput => JsonDocument.Parse("{}").RootElement;

    [Theory]
    [InlineData("Bash", false)]
    [InlineData("Read", false)]
    [InlineData("Write", true)]
    [InlineData("Grep", true)]
    [InlineData("PowerShell", false)]
    [InlineData("TaskCreate", true)]
    public void Everyone_Is_Blocked_From_Host_Tools_By_Default(string tool, bool isAdmin)
    {
        var policy = new MemberHostToolPolicy(new Principal(isAdmin), new Settings());
        Assert.Equal(HostToolAccess.HostBlockedMessage, policy.GetBlockReason(tool));
    }

    [Theory]
    [InlineData("WebSearch")]
    [InlineData("Artifact")]
    [InlineData("DocumentGenerate")]
    [InlineData("mcp__github__list_issues")]
    public void Other_Tools_Are_Unaffected(string tool) =>
        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: false), new Settings()).GetBlockReason(tool));

    [Fact]
    public async Task With_The_Server_Switch_On_Admins_Can_Use_Them_And_Members_Still_Cannot()
    {
        var settings = await SettingsWith(host: true, members: false);
        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: true), settings).GetBlockReason("Bash"));
        Assert.Equal(HostToolAccess.MemberBlockedMessage,
            new MemberHostToolPolicy(new Principal(isAdmin: false), settings).GetBlockReason("Bash"));
    }

    [Fact]
    public async Task Members_Need_Both_Switches()
    {
        Assert.Null(new MemberHostToolPolicy(new Principal(isAdmin: false), await SettingsWith(host: true, members: true))
            .GetBlockReason("Bash"));
        // The member switch alone does nothing while the server switch is off.
        Assert.Equal(HostToolAccess.HostBlockedMessage,
            new MemberHostToolPolicy(new Principal(isAdmin: false), await SettingsWith(host: false, members: true))
                .GetBlockReason("Bash"));
    }

    [Fact]
    public void Both_Switches_Are_Off_By_Default()
    {
        var config = GovernanceConfig.Load(new Settings());
        Assert.False(config.HostFileTools);
        Assert.False(config.MemberFileTools);
    }

    [Fact]
    public async Task Blocked_Tools_Are_Hidden_From_The_Model()
    {
        var registry = new InMemoryToolRegistry();
        foreach (var name in new[] { "Bash", "Read", "WebSearch", "Artifact" })
            registry.Register(new ToolDefinition(name, EmptyInput), (_, _) => Task.FromResult("ok"));

        var hidden = new HostPolicyToolRegistry(registry, new MemberHostToolPolicy(new Principal(isAdmin: true), new Settings()));
        Assert.Equal(["Artifact", "WebSearch"], hidden.GetDefinitions().Select(d => d.Name).Order());

        var allowed = new HostPolicyToolRegistry(registry,
            new MemberHostToolPolicy(new Principal(isAdmin: true), await SettingsWith(host: true, members: false)));
        Assert.Equal(["Artifact", "Bash", "Read", "WebSearch"], allowed.GetDefinitions().Select(d => d.Name).Order());
    }

    private static async Task<Settings> SettingsWith(bool host, bool members)
    {
        var settings = new Settings();
        var config = GovernanceConfig.Load(settings);
        config.HostFileTools = host;
        config.MemberFileTools = members;
        await config.SaveToStoreAsync(settings);
        return settings;
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
        Assert.Equal(HostToolAccess.HostBlockedMessage, result.Output);
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
