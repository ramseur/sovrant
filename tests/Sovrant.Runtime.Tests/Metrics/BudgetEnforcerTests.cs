using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Metrics;

namespace Sovrant.Runtime.Tests.Metrics;

public sealed class BudgetEnforcerTests
{
    [Fact]
    public void RecordSpend_UnderBudget_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 5.00m);

        var status = enforcer.RecordSpend("session-1", 1.00m);
        Assert.Equal(BudgetStatus.Ok, status);
    }

    [Fact]
    public void RecordSpend_ExceedsSessionBudget_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 2.00m);

        enforcer.RecordSpend("session-1", 1.50m);
        var status = enforcer.RecordSpend("session-1", 0.60m);

        Assert.Equal(BudgetStatus.Exceeded, status);
    }

    [Fact]
    public void RecordSpend_ExceedsProjectBudget_ReturnsExceeded()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            projectBudgetUsd: 10.00m);

        enforcer.RecordSpend("session-1", 6.00m);
        enforcer.RecordSpend("session-2", 5.00m);

        Assert.Equal(BudgetStatus.Exceeded, enforcer.Check("session-3"));
    }

    [Fact]
    public void RecordSpend_SessionBudget_IndependentPerSession()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 3.00m);

        enforcer.RecordSpend("session-1", 2.50m);
        var status = enforcer.RecordSpend("session-2", 1.00m);

        Assert.Equal(BudgetStatus.Ok, status);
    }

    [Fact]
    public void Check_NoSpend_ReturnsOk()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 5.00m);

        Assert.Equal(BudgetStatus.Ok, enforcer.Check("session-1"));
    }

    [Fact]
    public void NoBudget_IsEnabled_ReturnsFalse()
    {
        var enforcer = new BudgetEnforcer(NullLogger<BudgetEnforcer>.Instance);
        Assert.False(enforcer.IsEnabled);
    }

    [Fact]
    public void GetSessionSpend_TracksCorrectly()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            sessionBudgetUsd: 100m);

        enforcer.RecordSpend("s1", 1.5m);
        enforcer.RecordSpend("s1", 2.3m);

        Assert.Equal(3.8m, enforcer.GetSessionSpend("s1"));
        Assert.Equal(0m, enforcer.GetSessionSpend("s2"));
    }

    [Fact]
    public void ProjectSpend_AccumulatesAcrossSessions()
    {
        var enforcer = new BudgetEnforcer(
            NullLogger<BudgetEnforcer>.Instance,
            projectBudgetUsd: 100m);

        enforcer.RecordSpend("s1", 3.0m);
        enforcer.RecordSpend("s2", 2.0m);

        Assert.Equal(5.0m, enforcer.ProjectSpend);
    }
}
