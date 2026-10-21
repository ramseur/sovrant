using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Auth;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Tools;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// Phase 145 Part E: many people chatting at the same moment on one shared runtime (embedded Web).
/// Every turn runs on shared singletons — the provider, the tool registry, the host-tool policy — with
/// the person and conversation carried in async-local state. Turns are interleaved on purpose; each
/// request that reaches the model must still carry that person's model, identity, conversation and
/// tool list, never someone else's.
/// </summary>
public sealed class ConcurrentUsersTests
{
    [Fact]
    public async Task Fifty_People_Chatting_At_Once_Never_See_Each_Others_Model_Identity_Or_Tools()
    {
        const int people = 50;
        const int turnsEach = 3;
        var provider = new RecordingProvider();
        var registry = new InMemoryToolRegistry();
        foreach (var name in new[] { "Bash", "Read", "WebSearch", "Artifact" })
            registry.Register(new ToolDefinition(name, JsonDocument.Parse("{}").RootElement), (_, _) => Task.FromResult("ok"));
        // File/shell tools allowed on this server for admins only: the visible tool list depends on who's asking.
        var tools = new HostPolicyToolRegistry(registry, new MemberHostToolPolicy(AmbientPrincipal.Accessor, new AdminsOnlySettings()));
        var router = new OneProvider(provider);

        await Task.WhenAll(Enumerable.Range(0, people).Select(i => Task.Run(async () =>
        {
            var user = $"user{i}@example.com";
            var isAdmin = i % 5 == 0;
            var session = new SessionConfig { SessionId = $"s-{i}", OwnerUserId = user, Model = $"model-{i}" };
            // As in Chat.razor: the person is set before their conversation's runtime is created (its
            // system prompt is built then).
            using var asUser = AmbientPrincipal.Push(user, isAdmin ? "admin" : "user");
            var runtime = new ConversationRuntime(router, new ToolCountCapTests.StubToolExecutor(), tools,
                new ToolCountCapTests.InMemorySessionStore(), new SovrantConfig { Model = "install-default" },
                NullLogger<ConversationRuntime>.Instance);
            await runtime.InitializeSessionAsync(session.SessionId);

            for (var turn = 0; turn < turnsEach; turn++)
            {
                using var _ = AmbientPrincipal.Push(user, isAdmin ? "admin" : "user");
                using var __ = SessionContext.Push(session);
                await foreach (var _e in runtime.RunTurnAsync($"{user} turn {turn}")) { }
            }
        })));

        Assert.Equal(people * turnsEach, provider.Seen.Count);
        foreach (var r in provider.Seen)
        {
            var i = int.Parse(r.LastUserText.Split('@')[0]["user".Length..], System.Globalization.CultureInfo.InvariantCulture);
            var user = $"user{i}@example.com";
            Assert.Equal($"model-{i}", r.Model);                 // their model, not the install default or a neighbour's
            Assert.Equal(user, r.AmbientUser);                     // the person the turn runs as
            Assert.Equal(user, r.SessionOwner);
            Assert.StartsWith(user, r.LastUserText, StringComparison.Ordinal); // their conversation's message
            Assert.All(r.History, m => Assert.StartsWith(user, m, StringComparison.Ordinal)); // no one else's messages
            // The runtime sends only the tools relevant to each message, so check what each person was
            // told is available: admins have file tools on this server, members never get them.
            Assert.Equal(i % 5 == 0, r.FileToolsInPrompt);
            if (i % 5 != 0)
                Assert.DoesNotContain("Bash", r.Tools);            // a member never gets an admin's tool list
        }
    }

    private sealed record Seen(string Model, string? AmbientUser, string? SessionOwner, string LastUserText,
        IReadOnlyList<string> History, IReadOnlyList<string> Tools, bool FileToolsInPrompt);

    private sealed class RecordingProvider : ILlmProvider
    {
        public ConcurrentBag<Seen> Seen { get; } = [];
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Yield so other people's turns interleave with this one.
            await Task.Delay(Random.Shared.Next(1, 15), ct);
            var userTexts = req.Messages.Where(m => m.Role == "user")
                .Select(m => string.Concat(m.Content.OfType<InputContentBlock.TextBlock>().Select(t => t.Text))).ToList();
            Seen.Add(new Seen(req.Model, AmbientPrincipal.Current?.UserId, SessionContext.Current?.OwnerUserId,
                userTexts[^1], userTexts, req.Tools?.Select(t => t.Name).ToList() ?? [],
                req.System?.Contains("The Write tool is ONLY", StringComparison.Ordinal) == true));
            await Task.Delay(Random.Shared.Next(1, 15), ct);

            yield return new StreamEvent.MessageStart(new MessageResponse("m1", "message", "assistant", [], req.Model, new Usage(InputTokens: 1)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta($"{userTexts[^1]} — answered"));
            yield return new StreamEvent.ContentBlockStop(0);
            yield return new StreamEvent.MessageDelta(new Sovrant.Api.Types.MessageDelta("end_turn", null), new Usage(InputTokens: 1, OutputTokens: 1));
            yield return new StreamEvent.MessageStop();
        }
    }

    private sealed class OneProvider(ILlmProvider provider) : ISmartRouter
    {
        public bool IntentRoutingEnabled { get; set; }
        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<ILlmProvider> RouteAsync(MessagesRequest req, CancellationToken ct = default) => Task.FromResult(provider);
        public Task RecordResultAsync(string providerName, bool success, double durationMs, CancellationToken ct = default) => Task.CompletedTask;
        public IReadOnlyList<ProviderStatus> GetStatus() => [];
        public Task PinProviderAsync(string? providerName, CancellationToken ct = default) => Task.CompletedTask;
        public Task<RoutingDecision> RouteWithIntentAsync(MessagesRequest req, CancellationToken ct = default) =>
            Task.FromResult(new RoutingDecision(provider, null, null, null));
    }

    /// <summary>"Allow file and shell tools on this server" on, member switch off.</summary>
    private sealed class AdminsOnlySettings : IWorkspaceSettingsStore
    {
        public Task<string?> GetGlobalAsync(string key, CancellationToken ct = default) =>
            Task.FromResult<string?>(key == WorkspaceSettingsKeys.GovernanceHostFileTools ? "true" : null);
        public Task<string?> GetAsync(string workspaceId, string key, CancellationToken ct = default) => GetGlobalAsync(key, ct);
        public Task SetAsync(string workspaceId, string key, string value, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string workspaceId, string key, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(string workspaceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }
}
