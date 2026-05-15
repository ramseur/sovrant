using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace Sovrant.Tools.Quality;

/// <summary>Result of a single verification phase.</summary>
public sealed record PhaseResult(
    VerificationPhase Phase,
    bool Passed,
    string Output,
    bool Skipped = false)
{
    public string Status => Skipped ? "SKIP" : Passed ? "PASS" : "FAIL";
}

/// <summary>Aggregate result of the full verification pipeline.</summary>
public sealed class VerificationResult
{
    public Collection<PhaseResult> Phases { get; } = [];

    public bool AllPassed => Phases.All(p => p.Passed || p.Skipped);

    public string ProjectType { get; set; } = "unknown";

    public string ToReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Verification Report ({ProjectType})");
        sb.AppendLine();

        var maxName = Phases.Count > 0
            ? Phases.Max(p => p.Phase.ToString().Length)
            : 0;

        foreach (var phase in Phases)
        {
            var icon = phase.Skipped ? "⊘" : phase.Passed ? "✓" : "✗";
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {icon} {phase.Phase.ToString().PadRight(maxName)}  {phase.Status}");
            if (!phase.Passed && !phase.Skipped && !string.IsNullOrWhiteSpace(phase.Output))
            {
                foreach (var line in phase.Output.TrimEnd().Split('\n'))
                    sb.AppendLine(CultureInfo.InvariantCulture, $"    {line}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(AllPassed ? "All gates passed." : "One or more gates failed.");
        return sb.ToString();
    }
}
