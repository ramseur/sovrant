namespace Sovrant.Agents.Swarm;

/// <summary>
/// Post-execution reviewer that scores a swarm's combined output against
/// the original goal. Extracted as an interface so orchestrators can be
/// tested with deterministic fake verdicts.
/// </summary>
public interface ISwarmQualityGate
{
    /// <summary>Reviews the combined output and returns a quality verdict.</summary>
    Task<QualityVerdict> ReviewAsync(
        string swarmId,
        string originalPrompt,
        string combinedOutput,
        CancellationToken ct = default);
}
