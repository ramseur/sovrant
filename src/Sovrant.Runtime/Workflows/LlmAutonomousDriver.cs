namespace Sovrant.Runtime.Workflows;

/// <summary>
/// Phase 67 — the default <see cref="IAutonomousDriver"/>. A thin adapter
/// around the existing <see cref="IWorkflowExecutor"/>, so the LLM plan →
/// engine run → acceptance gate pipeline is the strategy chosen when a
/// workflow does not pin a specific driver name.
/// </summary>
public sealed class LlmAutonomousDriver : IAutonomousDriver
{
    public const string DriverName = "llm";

    private readonly IWorkflowExecutor _executor;

    public LlmAutonomousDriver(IWorkflowExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public string Name => DriverName;

    public DriverCapabilities Capabilities { get; } = new(
        SupportsReplanning: true,
        SupportsParallelSteps: false,
        SupportsHumanAcceptance: true,
        MaxStepsPerCycle: 1);

    public Task<Workflow> AdvanceAsync(string workflowId, CancellationToken ct = default) =>
        _executor.RunAsync(workflowId, ct);
}
