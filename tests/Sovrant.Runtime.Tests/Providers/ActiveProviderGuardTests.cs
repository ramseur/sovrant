using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Tools;
using static Sovrant.Runtime.Tests.Conversation.ToolCountCapTests;

namespace Sovrant.Runtime.Tests.Providers;

/// <summary>
/// Phase 138 — the running provider follows the active workspace: a saved profile that isn't
/// enabled for the workspace is switched off (and never contacted), and comes back when it is.
/// </summary>
public sealed class ActiveProviderGuardTests
{
    private sealed class MemoryCredentials : ICredentialStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
        public Task StoreAsync(string key, string value, CancellationToken ct = default) { _values[key] = value; return Task.CompletedTask; }
        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default) => Task.FromResult(_values.GetValueOrDefault(key));
        public Task DeleteAsync(string key, CancellationToken ct = default) { _values.Remove(key); return Task.CompletedTask; }
    }

    private static readonly Uri OllamaUrl = new("http://localhost:11434/v1");

    private static (SovrantConfig Config, List<(string Key, Uri? Url)> AuthCalls) Running()
    {
        var config = new SovrantConfig { Model = "llama3", ApiKey = "k" };
        config.BaseUrl = OllamaUrl;
        return (config, []);
    }

    [Fact]
    public async Task Profile_Not_Enabled_For_Workspace_Is_Switched_Off()
    {
        var (config, auth) = Running();

        var changed = await ActiveProviderGuard.ReconcileAsync(config, "ollama-profile", available: null, "llama3",
            new MemoryCredentials(), (k, u) => auth.Add((k, u)));

        Assert.True(changed);
        Assert.Null(config.BaseUrl);
        Assert.Null(config.ApiKey);
        Assert.Equal(ActiveProviderGuard.NotEnabledReason, config.InactiveProviderReason);
        Assert.Equal([(string.Empty, (Uri?)null)], auth);
    }

    [Fact]
    public async Task Switching_Back_To_A_Workspace_Where_It_Is_Enabled_Reactivates_It()
    {
        var (config, auth) = Running();
        var creds = new MemoryCredentials();
        await creds.StoreAsync("provider.ollama.api_key", "local-key");
        await ActiveProviderGuard.ReconcileAsync(config, "ollama-profile", null, "llama3", creds, (_, _) => { });

        var changed = await ActiveProviderGuard.ReconcileAsync(config, "ollama-profile",
            ActiveProviderGuard.AvailableProfile.From("provider.ollama.api_key", "http://localhost:11434/v1", 16000),
            "llama3:8b", creds, (k, u) => auth.Add((k, u)));

        Assert.True(changed);
        Assert.Null(config.InactiveProviderReason);
        Assert.Equal(OllamaUrl, config.BaseUrl);
        Assert.Equal("local-key", config.ApiKey);
        Assert.Equal("llama3:8b", config.Model);
        Assert.Equal(16000, config.MaxTokens);
        Assert.Equal([("local-key", (Uri?)OllamaUrl)], auth);
    }

    [Fact]
    public async Task Available_And_Already_Active_Is_Left_Alone()
    {
        var (config, auth) = Running();

        var changed = await ActiveProviderGuard.ReconcileAsync(config, "p",
            ActiveProviderGuard.AvailableProfile.From(null, "https://openrouter.ai/api/v1", 32000), "m",
            new MemoryCredentials(), (k, u) => auth.Add((k, u)));

        Assert.False(changed);
        Assert.Equal(OllamaUrl, config.BaseUrl);
        Assert.Empty(auth);
    }

    [Fact]
    public async Task No_Saved_Profile_Does_Nothing()
    {
        var (config, auth) = Running();

        Assert.False(await ActiveProviderGuard.ReconcileAsync(config, null, null, null, new MemoryCredentials(), (k, u) => auth.Add((k, u))));
        Assert.Equal(OllamaUrl, config.BaseUrl);
        Assert.Null(config.InactiveProviderReason);
    }

    [Fact]
    public async Task Keyless_Local_Profile_Reactivates_Without_A_Key()
    {
        var config = new SovrantConfig();
        config.DeactivateProvider(ActiveProviderGuard.NotEnabledReason);

        await ActiveProviderGuard.ReconcileAsync(config, "lm",
            ActiveProviderGuard.AvailableProfile.From(credentialId: null, "http://localhost:1234/v1", 32000), null,
            new MemoryCredentials(), (_, _) => { });

        Assert.Equal(new Uri("http://localhost:1234/v1"), config.BaseUrl);
        Assert.Equal(string.Empty, config.ApiKey);
        Assert.Null(config.InactiveProviderReason);
    }

    [Fact]
    public void Activating_Any_Profile_Clears_The_Inactive_Reason()
    {
        var config = new SovrantConfig();
        config.DeactivateProvider("off");
        config.BaseUrl = new Uri("https://api.openai.com/v1");
        Assert.Null(config.InactiveProviderReason);
    }

    [Fact]
    public async Task Turn_Fails_With_The_Reason_And_Never_Reaches_A_Provider()
    {
        var router = new FakeRouter();
        var config = new SovrantConfig { Model = "test-model" };
        config.DeactivateProvider(ActiveProviderGuard.NotEnabledReason);
        var runtime = new ConversationRuntime(
            router: router,
            toolExecutor: new StubToolExecutor(),
            toolRegistry: new InMemoryToolRegistry(),
            sessionStore: new InMemorySessionStore(),
            config: config,
            logger: NullLogger<ConversationRuntime>.Instance);
        await runtime.InitializeSessionAsync("sess-inactive");
        using var _ = SessionContext.Push(new SessionConfig());

        var events = new List<RuntimeEvent>();
        await foreach (var e in runtime.RunTurnAsync("hello"))
            events.Add(e);

        var error = Assert.Single(events.OfType<RuntimeEvent.RuntimeError>());
        Assert.Equal(ActiveProviderGuard.NotEnabledReason, error.Message);
        Assert.Null(router.Provider.LastTools); // the provider was never called
        Assert.DoesNotContain(events, e => e is RuntimeEvent.TextChunk);
    }
}
