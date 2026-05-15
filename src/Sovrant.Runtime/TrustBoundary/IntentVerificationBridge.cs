using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.TrustBoundary;

/// <summary>Result of the intent verification stage in the trust pipeline.</summary>
public abstract record IntentVerificationResult
{
    private IntentVerificationResult() { }

    /// <summary>Intent is clear and benign — proceed to ethical harness and sanitizer.</summary>
    public sealed record Proceed(IntentGateResult GateResult) : IntentVerificationResult;

    /// <summary>Intent is ambiguous — needs clarification before any provider call.</summary>
    public sealed record NeedsClarification(string Question) : IntentVerificationResult;

    /// <summary>Intent is clearly harmful — block immediately without reaching provider.</summary>
    public sealed record BlockedHarmful(string Reason) : IntentVerificationResult;
}

/// <summary>
/// Bridges Phase 59's <see cref="IIntentGate"/> into the trust boundary pipeline.
/// This is the first stage: classify intent, clarify ambiguity, fast-path block harmful intent.
/// </summary>
public sealed class IntentVerificationBridge
{
    private readonly IIntentGate? _intentGate;
    private readonly IEthicalHarness? _ethicalHarness;

    public IntentVerificationBridge(IIntentGate? intentGate = null, IEthicalHarness? ethicalHarness = null)
    {
        _intentGate = intentGate;
        _ethicalHarness = ethicalHarness;
    }

    /// <summary>
    /// Verifies user intent before any data touches the sanitizer or provider.
    /// </summary>
    /// <param name="userMessage">The raw user message text.</param>
    /// <param name="conversationDepth">Number of prior messages in the conversation.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IntentVerificationResult> VerifyAsync(
        string userMessage, int conversationDepth = 0, CancellationToken ct = default)
    {
        // Stage 1: Quick ethical check on the raw user message.
        // This catches obviously harmful prompts before we even classify intent.
        if (_ethicalHarness is not null)
        {
            var ethicalVerdict = _ethicalHarness.EvaluateOutbound(userMessage);
            if (ethicalVerdict is EthicalVerdict.Block block)
                return new IntentVerificationResult.BlockedHarmful(block.Reason);
        }

        // Stage 2: Classify intent via the Phase 59 gate.
        if (_intentGate is not null)
        {
            var gateResult = await _intentGate.ClassifyAsync(userMessage, conversationDepth, ct)
                .ConfigureAwait(false);

            if (gateResult.NeedsClarification && gateResult.SuggestedClarification is not null)
                return new IntentVerificationResult.NeedsClarification(gateResult.SuggestedClarification);

            return new IntentVerificationResult.Proceed(gateResult);
        }

        // No intent gate configured — proceed unconditionally.
        return new IntentVerificationResult.Proceed(
            new IntentGateResult(Sovrant.Api.Routing.IntentClass.Conversation, 0.5f, false, false, null));
    }
}
