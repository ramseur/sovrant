namespace Sovrant.Runtime.TrustBoundary;

/// <summary>
/// Evaluates content against Sovrant's ethical policy.
/// This is the engine-level safety layer — it works regardless of which model or
/// provider is behind the request, ensuring consistent ethical guarantees.
/// </summary>
public interface IEthicalHarness
{
    /// <summary>Evaluates an outbound prompt before it reaches the provider.</summary>
    EthicalVerdict EvaluateOutbound(string text);

    /// <summary>Evaluates an inbound response from the provider before it reaches the user.</summary>
    EthicalVerdict EvaluateInbound(string text);
}
