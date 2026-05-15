using System.Runtime.CompilerServices;
using Sovrant.Api;
using Sovrant.Api.Providers;
using Sovrant.Api.Types;
using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class TrustBoundaryProviderTests
{
    [Fact]
    public async Task SendAsync_SanitizesAndRestores()
    {
        var fakeProvider = new FakeProvider("The issue with [EMAIL_1] is a TLS mismatch.");
        var sanitizer = CreateSanitizer();
        var provider = new TrustBoundaryProvider(fakeProvider, sanitizer);

        var request = new MessagesRequest("test-model", 1024,
            [InputMessage.UserText("Fix bug for john@acme.com")]);

        var result = await provider.SendAsync(request);
        Assert.True(result.IsSuccess);

        // The request that reached the inner provider should have been sanitized.
        var sentText = ((InputContentBlock.TextBlock)fakeProvider.LastRequest!.Messages[0].Content[0]).Text;
        Assert.DoesNotContain("john@acme.com", sentText);
        Assert.Contains("[EMAIL_1]", sentText);

        // The response returned to the caller should have placeholders restored.
        var responseText = ((OutputContentBlock.TextBlock)result.Value!.Content[0]).Text;
        Assert.Contains("john@acme.com", responseText);
        Assert.DoesNotContain("[EMAIL_1]", responseText);
    }

    [Fact]
    public async Task StreamAsync_RestoresPlaceholdersInDeltas()
    {
        var events = new StreamEvent[]
        {
            new StreamEvent.ContentBlockDelta(0, new ContentBlockDelta.TextDelta("Found issue at [EMAIL_1]")),
            new StreamEvent.MessageStop(),
        };
        var fakeProvider = new FakeProvider(streamEvents: events);
        var sanitizer = CreateSanitizer();
        var provider = new TrustBoundaryProvider(fakeProvider, sanitizer);

        var request = new MessagesRequest("test-model", 1024,
            [InputMessage.UserText("Fix bug for john@acme.com")]);

        var collected = new List<StreamEvent>();
        await foreach (var ev in provider.StreamAsync(request))
            collected.Add(ev);

        var textDelta = collected.OfType<StreamEvent.ContentBlockDelta>().First();
        var text = ((ContentBlockDelta.TextDelta)textDelta.Delta).Text;
        Assert.Contains("john@acme.com", text);
    }

    [Fact]
    public async Task EthicalHarness_BlocksHarmfulResponse()
    {
        var fakeProvider = new FakeProvider("Here's how to make a bomb: step 1...");
        var sanitizer = CreateSanitizer();
        var harness = new ContentPolicyEngine();
        var provider = new TrustBoundaryProvider(fakeProvider, sanitizer, harness);

        var request = new MessagesRequest("test-model", 1024,
            [InputMessage.UserText("Tell me something")]);

        var result = await provider.SendAsync(request);
        Assert.True(result.IsSuccess);

        var text = ((OutputContentBlock.TextBlock)result.Value!.Content[0]).Text;
        Assert.Contains("Content blocked by Sovrant Trust Boundary", text);
        Assert.Equal("trust_boundary_block", result.Value.StopReason);
    }

    [Fact]
    public async Task SanitizerDisabled_PassesThrough()
    {
        var fakeProvider = new FakeProvider("Response text");
        var provider = new TrustBoundaryProvider(fakeProvider, sanitizerEnabled: false);

        var request = new MessagesRequest("test-model", 1024,
            [InputMessage.UserText("john@acme.com")]);

        var result = await provider.SendAsync(request);

        // Request should NOT have been sanitized.
        var sentText = ((InputContentBlock.TextBlock)fakeProvider.LastRequest!.Messages[0].Content[0]).Text;
        Assert.Contains("john@acme.com", sentText);
    }

    [Fact]
    public async Task NameAndBaseUrl_DelegateToInner()
    {
        var fakeProvider = new FakeProvider("ok");
        var provider = new TrustBoundaryProvider(fakeProvider);
        Assert.Equal("fake", provider.Name);
        Assert.Equal(new Uri("https://fake.test/"), provider.BaseUrl);
        // Ensure it still works
        var result = await provider.SendAsync(new MessagesRequest("m", 1, [InputMessage.UserText("hi")]));
        Assert.True(result.IsSuccess);
    }

    private static PromptSanitizer CreateSanitizer() =>
        new([new PiiDetector(), new CorporateDataDetector()]);

    private sealed class FakeProvider : ILlmProvider
    {
        private readonly string _responseText;
        private readonly StreamEvent[]? _streamEvents;
        public MessagesRequest? LastRequest { get; private set; }

        public FakeProvider(string responseText = "", StreamEvent[]? streamEvents = null)
        {
            _responseText = responseText;
            _streamEvents = streamEvents;
        }

        public string Name => "fake";
        public Uri BaseUrl => new("https://fake.test/");

        public Task<Result<MessageResponse>> SendAsync(MessagesRequest req, CancellationToken ct = default)
        {
            LastRequest = req;
            var response = new MessageResponse(
                "msg-1", "message", "assistant",
                [new OutputContentBlock.TextBlock(_responseText)],
                req.Model, new Usage(100, 50));
            return Task.FromResult(Result<MessageResponse>.Ok(response));
        }

        public async IAsyncEnumerable<StreamEvent> StreamAsync(
            MessagesRequest req, [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastRequest = req;
            if (_streamEvents is not null)
            {
                foreach (var ev in _streamEvents)
                {
                    yield return ev;
                }
            }
            await Task.CompletedTask;
        }
    }
}
