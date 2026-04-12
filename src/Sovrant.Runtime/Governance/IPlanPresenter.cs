using Sovrant.Runtime.Engine;

namespace Sovrant.Runtime.Governance;

/// <summary>
/// Phase 59b — formats a <see cref="RuntimePlan"/> into a user-readable
/// summary that can be presented for review before execution begins.
/// </summary>
public interface IPlanPresenter
{
    /// <summary>
    /// Formats the plan as a numbered step list suitable for display in
    /// CLI, desktop, or web UIs.
    /// </summary>
    string FormatPlan(RuntimePlan plan);
}
