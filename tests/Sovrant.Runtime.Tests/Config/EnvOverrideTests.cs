using Sovrant.Api.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Config;

/// <summary>
/// Phase 148 — one switch (SOVRANT_ENV_OVERRIDE) for how environment variables relate to what admins
/// set in the app. Off: env seeds the database once and the stored value wins. On: env wins, and the
/// app shows the control locked (saving is refused). Same rule for settings and provider keys.
/// </summary>
public sealed class EnvOverrideTests
{
    private static Func<string, string?> Env(params (string Key, string Value)[] vars)
    {
        var d = vars.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
        return k => d.GetValueOrDefault(k);
    }

    [Fact]
    public void One_Switch_With_The_Older_Keys_Name_Still_Accepted()
    {
        Assert.False(EnvOverride.IsOn(Env()));
        Assert.True(EnvOverride.IsOn(Env((EnvOverride.Variable, "true"))));
        Assert.True(EnvOverride.IsOn(Env((EnvOverride.LegacyKeysVariable, "1"))));
        Assert.True(EnvCredentialSeeder.OverrideEnabled(Env((EnvOverride.Variable, "TRUE"))));
    }

    [Fact]
    public async Task Off_Env_Seeds_Once_And_The_Admins_Value_Then_Wins()
    {
        var store = new MemorySettings();
        var env = Env(("SOVRANT_GOVERNANCE_AUDIT_LOG", "false"), ("SOVRANT_MAX_RUN_MINUTES", "60"), ("SOVRANT_WEB_REMEMBER_DAYS", "0"));

        var seeded = await EnvBackedSettings.SeedAsync(store, env);

        Assert.Contains(WorkspaceSettingsKeys.GovernanceAuditLog, seeded);
        Assert.Equal("false", await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceAuditLog));
        Assert.Equal("60", await store.GetGlobalAsync(WorkspaceSettingsKeys.RunMaxMinutes));
        Assert.Equal("false", await store.GetGlobalAsync(WorkspaceSettingsKeys.WebSignInRememberAllowed)); // 0 days = off
        Assert.Null(await store.GetGlobalAsync(WorkspaceSettingsKeys.WebSignInRememberDays));

        // The admin changes it in the app; a later start doesn't overwrite it.
        await store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.GovernanceAuditLog, "true");
        Assert.DoesNotContain(WorkspaceSettingsKeys.GovernanceAuditLog, await EnvBackedSettings.SeedAsync(store, env));
        Assert.Equal("true", await store.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceAuditLog));
        Assert.Null(EnvBackedSettings.LockedBy(WorkspaceSettingsKeys.GovernanceAuditLog, env)); // not locked
    }

    [Fact]
    public async Task On_Env_Is_Not_Seeded_The_Control_Is_Locked_And_Saving_Is_Refused()
    {
        var env = Env((EnvOverride.Variable, "true"), ("SOVRANT_GOVERNANCE_AUDIT_LOG", "false"));
        var inner = new MemorySettings();
        var store = new EnvLockedSettingsStore(inner, env);

        Assert.Empty(await EnvBackedSettings.SeedAsync(store, env));
        Assert.Equal("SOVRANT_GOVERNANCE_AUDIT_LOG", EnvBackedSettings.LockedBy(WorkspaceSettingsKeys.GovernanceAuditLog, env));
        Assert.Null(EnvBackedSettings.LockedBy(WorkspaceSettingsKeys.GovernanceLevel, env)); // its variable isn't set

        await store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.GovernanceAuditLog, "true");
        await store.SetAsync(WorkspaceSettingsKeys.GlobalWorkspaceId, WorkspaceSettingsKeys.GovernanceLevel, "strict");
        Assert.Null(await inner.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceAuditLog)); // refused
        Assert.Equal("strict", await inner.GetGlobalAsync(WorkspaceSettingsKeys.GovernanceLevel)); // not locked: saved

        var lockInfo = new EnvLock(EnvBackedSettings.LockedBy(WorkspaceSettingsKeys.GovernanceAuditLog, env));
        Assert.True(lockInfo.IsLocked);
        Assert.Equal("Set by the server's environment (SOVRANT_GOVERNANCE_AUDIT_LOG). To change it here, turn off SOVRANT_ENV_OVERRIDE.", lockInfo.Note);
    }

    [Fact]
    public void Provider_Keys_From_The_Environment_Lock_With_The_Same_Switch()
    {
        Assert.Null(EnvCredentialSeeder.LockedBy(CredentialKeys.LlmApiKey, Env(("LLM_API_KEY", "x")))); // override off
        Assert.Equal("LLM_API_KEY", EnvCredentialSeeder.LockedBy(CredentialKeys.LlmApiKey, Env((EnvOverride.Variable, "true"), ("LLM_API_KEY", "x"))));
        Assert.Null(EnvCredentialSeeder.LockedBy(CredentialKeys.BraveApiKey, Env((EnvOverride.Variable, "true"), ("LLM_API_KEY", "x"))));
    }

    [Fact]
    public async Task Run_Limit_And_Sign_In_Follow_The_Switch()
    {
        var store = new MemorySettings();
        await Sovrant.Runtime.Conversation.RunLimits.SaveAsync(store, 240);

        Assert.Equal(TimeSpan.FromHours(4), Sovrant.Runtime.Conversation.RunLimits.Load(store, Env(("SOVRANT_MAX_RUN_MINUTES", "30"))));
        Assert.Equal(TimeSpan.FromMinutes(30), Sovrant.Runtime.Conversation.RunLimits.Load(store,
            Env((EnvOverride.Variable, "true"), ("SOVRANT_MAX_RUN_MINUTES", "30"))));

        await new Sovrant.Runtime.Auth.WebSignInSettings(true, 7, 120, 8).SaveAsync(store);
        Assert.Equal(120, Sovrant.Runtime.Auth.WebSignInSettings.Load(store, Env(("SOVRANT_WEB_IDLE_MINUTES", "15"))).IdleMinutes);
        var locked = Sovrant.Runtime.Auth.WebSignInSettings.Load(store, Env((EnvOverride.Variable, "true"), ("SOVRANT_WEB_IDLE_MINUTES", "15")));
        Assert.Equal(15, locked.IdleMinutes);
        Assert.Equal(8, locked.MaxHours); // its variable isn't set: the admin's value
    }

    private sealed class MemorySettings : IWorkspaceSettingsStore
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
