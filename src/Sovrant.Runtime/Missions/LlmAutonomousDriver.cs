namespace Sovrant.Runtime.Missions;

/// <summary>
/// Phase 67 — the default <see cref="IAutonomousDriver"/>. A thin adapter
/// around the existing <see cref="IMissionExecutor"/>, so the LLM plan →
/// engine run → acceptance gate pipeline is the strategy chosen when a
/// mission does not pin a specific driver name.
/// </summary>
public sealed class LlmAutonomousDriver : IAutonomousDriver
{
    public const string DriverName = "llm";

    private readonly IMissionExecutor _executor;

    public LlmAutonomousDriver(IMissionExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public string Name => DriverName;

    public DriverCapabilities Capabilities { get; } = new(
        SupportsReplanning: true,
        SupportsParallelSteps: false,
        SupportsHumanAcceptance: true,
        MaxStepsPerCycle: 1);

    public Task<Mission> AdvanceAsync(string missionId, CancellationToken ct = default) =>
        _executor.RunAsync(missionId, ct);
}
