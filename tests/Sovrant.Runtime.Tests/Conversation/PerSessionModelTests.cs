using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Mcp;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// Phase 145 Part B — on a shared server each conversation uses its own model and its own (shared,
/// admin-configured) provider profile; nothing global is rewritten, so one member's pick never
/// changes anyone else's. Before this, the runtime always read the global model, so even Server's
/// per-session model was written and ignored.
/// </summary>
public sealed class PerSessionModelTests
{
    [Fact]
    public async Task A_Conversation_Uses_Its_Own_Model_And_Others_Keep_The_Default()
    {
        var provider = new ModelCapturingProvider();
        var runtime = Runtime(new SingleRouter(provider), "default-model");

        await DrainAsync(runtime, "hello");
        Assert.Equal("default-model", provider.LastModel);

        using (SessionContext.Push(new SessionConfig { Model = "member-pick" }))
            await DrainAsync(runtime, "hello again");
        Assert.Equal("member-pick", provider.LastModel);

        await DrainAsync(runtime, "and once more"); // outside that conversation: the default again
        Assert.Equal("default-model", provider.LastModel);
    }

    [Fact]
    public async Task A_Conversation_With_A_Profile_Goes_To_That_Profiles_Address()
    {
        var fallback = new SingleRouter(new ModelCapturingProvider());
        var profiles = new Profiles(new ProviderProfile("p-or", "admin@example.com", "OpenRouter", "OpenRouter",
            "https://openrouter.ai/api/v1", "openai/gpt-4o-mini", 32000, "provider.p-or.api_key", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var credentials = new Credentials { ["provider.p-or.api_key"] = "sk-or-shared" };
        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var router = new SessionProviderRouter(fallback, profiles, credentials,
            new DefaultScopedProviderFactory(NullLoggerFactory.Instance), services.GetRequiredService<IHttpClientFactory>());
        var req = new MessagesRequest("m", 100, []);

        Assert.Same(fallback.Provider, await router.RouteAsync(req)); // no conversation profile: the default

        using (SessionContext.Push(new SessionConfig { ProviderProfileId = "p-or" }))
        {
            var routed = await router.RouteAsync(req);
            Assert.Equal(new Uri("https://openrouter.ai/api/v1/"), routed.BaseUrl);
            Assert.Same(routed, await router.RouteAsync(req)); // cached

            credentials["provider.p-or.api_key"] = "sk-or-rotated"; // admin rotates the key
            Assert.NotSame(routed, await router.RouteAsync(req));
        }

        using (SessionContext.Push(new SessionConfig { ProviderProfileId = "deleted-profile" }))
            Assert.Same(fallback.Provider, await router.RouteAsync(req)); // gone: the default
    }

    private static ConversationRuntime Runtime(ISmartRouter router, string model) => new(
        router: router,
        toolExecutor: new ToolCountCapTests.StubToolExecutor(),
        toolRegistry: new InMemoryToolRegistry(),
        sessionStore: new ToolCountCapTests.InMemorySessionStore(),
        config: new SovrantConfig { Model = model },
        logger: NullLogger<ConversationRuntime>.Instance);

    private static async Task DrainAsync(ConversationRuntime runtime, string text)
    {
        await foreach (var _ in runtime.RunTurnAsync(text)) { }
    }

    private sealed class ModelCapturingProvider : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public string? LastModel { get; private set; }

        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastModel = req.Model;
            yield return new StreamEvent.MessageStart(new MessageResponse("m1", "message", "assistant", [], req.Model, new Usage(InputTokens: 1)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("ok"));
            yield return new StreamEvent.ContentBlockStop(0);
            yield return new StreamEvent.MessageDelta(new Sovrant.Api.Types.MessageDelta("end_turn", null), new Usage(InputTokens: 1, OutputTokens: 1));
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class SingleRouter(ILlmProvider provider) : ISmartRouter
    {
        public ILlmProvider Provider { get; } = provider;
        public bool IntentRoutingEnabled { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) => Task.FromResult(Provider);
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            Task.FromResult(new RoutingDecision(Provider, null, null, null));
    }

    private sealed class Profiles(params ProviderProfile[] all) : IProviderProfileStore
    {
        public Task<ProviderProfile?> GetAsync(string profileId, CancellationToken ct = default) =>
            Task.FromResult(all.FirstOrDefault(p => p.ProfileId == profileId));
        public Task<IReadOnlyList<ProviderProfile>> ListAsync(string userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProviderProfile>>(all);
        public Task<IReadOnlyList<ProviderProfile>> ListByWorkspaceAsync(string workspaceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProviderProfile>>([]);
        public Task CreateAsync(ProviderProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(ProviderProfile profile, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string profileId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Credentials : Dictionary<string, string>, ICredentialStore
    {
        public Task StoreAsync(string key, string value, CancellationToken ct = default) { this[key] = value; return Task.CompletedTask; }
        public Task<string?> RetrieveAsync(string key, CancellationToken ct = default) => Task.FromResult(TryGetValue(key, out var v) ? v : null);
        public Task DeleteAsync(string key, CancellationToken ct = default) { Remove(key); return Task.CompletedTask; }
    }
}
