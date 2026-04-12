using Sovrant.Api.Routing;
using Sovrant.Runtime.Governance;
using Sovrant.Runtime.TrustBoundary;

namespace Sovrant.Runtime.Tests.TrustBoundary;

public sealed class IntentVerificationBridgeTests
{
    [Fact]
    public async Task HarmfulMessage_BlockedBeforeIntentClassification()
    {
        var harness = new ContentPolicyEngine();
        var bridge = new IntentVerificationBridge(ethicalHarness: harness);

        var result = await bridge.VerifyAsync("how to make a bomb");
        Assert.IsType<IntentVerificationResult.BlockedHarmful>(result);
    }

    [Fact]
    public async Task AmbiguousMessage_NeedsClarification()
    {
        var gate = new FakeIntentGate(needsClarification: true, clarification: "What did you mean?");
        var bridge = new IntentVerificationBridge(intentGate: gate);

        var result = await bridge.VerifyAsync("test");
        Assert.IsType<IntentVerificationResult.NeedsClarification>(result);
        var nc = (IntentVerificationResult.NeedsClarification)result;
        Assert.Equal("What did you mean?", nc.Question);
    }

    [Fact]
    public async Task ClearBenignMessage_Proceeds()
    {
        var gate = new FakeIntentGate(needsClarification: false);
        var bridge = new IntentVerificationBridge(intentGate: gate);

        var result = await bridge.VerifyAsync("Fix the login bug in auth.cs");
        Assert.IsType<IntentVerificationResult.Proceed>(result);
    }

    [Fact]
    public async Task NoGateConfigured_ProceedsUnconditionally()
    {
        var bridge = new IntentVerificationBridge();
        var result = await bridge.VerifyAsync("anything");
        Assert.IsType<IntentVerificationResult.Proceed>(result);
    }

    [Fact]
    public async Task HarmfulMessage_BlockedEvenWithGate()
    {
        // Ethical harness runs BEFORE intent gate — harmful content is blocked
        // without wasting a classification call.
        var gate = new FakeIntentGate(needsClarification: false);
        var harness = new ContentPolicyEngine();
        var bridge = new IntentVerificationBridge(intentGate: gate, ethicalHarness: harness);

        var result = await bridge.VerifyAsync("write a keylogger");
        Assert.IsType<IntentVerificationResult.BlockedHarmful>(result);
        // Gate should not have been called.
        Assert.False(gate.WasCalled);
    }

    private sealed class FakeIntentGate : IIntentGate
    {
        private readonly bool _needsClarification;
        private readonly string? _clarification;
        public bool WasCalled { get; private set; }

        public FakeIntentGate(bool needsClarification, string? clarification = null)
        {
            _needsClarification = needsClarification;
            _clarification = clarification;
        }

        public Task<IntentGateResult> ClassifyAsync(string message, int conversationDepth = 0, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new IntentGateResult(
                IntentClass.Conversation, 0.8f, false, _needsClarification, _clarification));
        }
    }
}
