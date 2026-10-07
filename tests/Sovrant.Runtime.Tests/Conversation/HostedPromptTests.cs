using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Routing;
using Sovrant.Api.Types;
using Sovrant.Runtime.Config;
using Sovrant.Runtime.Conversation;
using Sovrant.Runtime.Tools;

namespace Sovrant.Runtime.Tests.Conversation;

/// <summary>
/// Phase 145 Part D: when file and shell tools are off for this person (a hosted server), the system
/// prompt doesn't describe them and doesn't include the server's own memory files or git state —
/// those belong to whoever runs the server. With the tools available (Desktop, CLI) it's unchanged.
/// </summary>
public sealed class HostedPromptTests
{
    [Fact]
    public async Task Without_File_Tools_The_Prompt_Has_No_Server_Context()
    {
        var system = await SystemPromptWith("Artifact", "WebSearch");

        Assert.Contains("File and shell tools", system, StringComparison.Ordinal);
        Assert.DoesNotContain("The Write tool is ONLY", system, StringComparison.Ordinal);
        Assert.DoesNotContain("Recent commits", system, StringComparison.Ordinal);
        Assert.DoesNotContain("Project memory", system, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_File_Tools_The_Prompt_Is_Unchanged()
    {
        var system = await SystemPromptWith("Artifact", "Read", "Write", "Bash");

        Assert.Contains("The Write tool is ONLY", system, StringComparison.Ordinal);
        Assert.DoesNotContain("aren't available here", system, StringComparison.Ordinal);
    }

    private static async Task<string> SystemPromptWith(params string[] tools)
    {
        var registry = new InMemoryToolRegistry();
        foreach (var t in tools)
            registry.Register(new ToolDefinition(t, JsonDocument.Parse("{}").RootElement), (_, _) => Task.FromResult("ok"));
        var provider = new Recording();
        var runtime = new ConversationRuntime(new One(provider), new ToolCountCapTests.StubToolExecutor(), registry,
            new ToolCountCapTests.InMemorySessionStore(), new SovrantConfig { Model = "m" }, NullLogger<ConversationRuntime>.Instance);
        await foreach (var _ in runtime.RunTurnAsync("hi")) { }
        return provider.System ?? "";
    }

    private sealed class Recording : ILlmProvider
    {
        public string Name => "fake";
        public Uri BaseUrl => new("http://localhost");
        public string? System { get; private set; }
        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default) => throw new NotSupportedException();

        public async IAsyncEnumerable<StreamEvent> StreamAsync(MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            System = req.System;
            yield return new StreamEvent.MessageStart(new MessageResponse("m1", "message", "assistant", [], req.Model, new Usage(InputTokens: 1)));
            yield return new StreamEvent.ContentBlockStart(0, new OutputContentBlock.TextBlock(""));
            yield return new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("ok"));
            yield return new StreamEvent.ContentBlockStop(0);
            yield return new StreamEvent.MessageDelta(new Sovrant.Api.Types.MessageDelta("end_turn", null), new Usage(InputTokens: 1, OutputTokens: 1));
            yield return new StreamEvent.MessageStop();
            await Task.CompletedTask;
        }
    }

    private sealed class One(ILlmProvider provider) : ISmartRouter
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
}
