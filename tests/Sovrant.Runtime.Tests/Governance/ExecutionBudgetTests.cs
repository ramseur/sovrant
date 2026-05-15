using Sovrant.Runtime.Governance;

namespace Sovrant.Runtime.Tests.Governance;

public sealed class ExecutionBudgetTests
{
    [Fact]
    public void NewBudget_WithinBudget()
    {
        var budget = new ExecutionBudget();
        Assert.Equal(ExecutionBudgetStatus.WithinBudget, budget.Status);
    }

    [Fact]
    public void MaxToolCalls_Exceeded()
    {
        var budget = new ExecutionBudget(new ExecutionBudgetConfig(MaxToolCalls: 3));

        Assert.True(budget.TryConsume("Read"));   // 1
        Assert.True(budget.TryConsume("Grep"));    // 2
        Assert.True(budget.TryConsume("Read"));    // 3 — at limit
        Assert.False(budget.TryConsume("Read"));   // 4 — exceeded

        Assert.Equal(ExecutionBudgetStatus.Exceeded, budget.Status);
        Assert.Equal(4, budget.ToolCallCount);
    }

    [Fact]
    public void MaxFilesModified_Exceeded()
    {
        var budget = new ExecutionBudget(new ExecutionBudgetConfig(MaxFilesModified: 2));

        Assert.True(budget.TryConsume("Write", "/a.cs"));   // 1 file
        Assert.True(budget.TryConsume("Edit", "/b.cs"));     // 2 files — at limit
        Assert.False(budget.TryConsume("Write", "/c.cs"));   // 3 files — exceeded

        Assert.Equal(2, budget.FilesModified);
    }

    [Fact]
    public void SameFile_CountsOnce()
    {
        var budget = new ExecutionBudget(new ExecutionBudgetConfig(MaxFilesModified: 2));

        Assert.True(budget.TryConsume("Write", "/a.cs"));
        Assert.True(budget.TryConsume("Edit", "/a.cs"));    // same file

        Assert.Equal(1, budget.FilesModified);
    }

    [Fact]
    public void SafeTools_DontCountAsFileModification()
    {
        var budget = new ExecutionBudget(new ExecutionBudgetConfig(MaxFilesModified: 1));

        Assert.True(budget.TryConsume("Read", "/a.cs"));

        Assert.Equal(0, budget.FilesModified);
    }

    [Fact]
    public void Summarize_ReturnsReadableString()
    {
        var budget = new ExecutionBudget(new ExecutionBudgetConfig(MaxToolCalls: 10, MaxFilesModified: 5));
        budget.TryConsume("Read");
        budget.TryConsume("Write", "/a.cs");

        var summary = budget.Summarize();

        Assert.Contains("Tool calls: 2/10", summary);
        Assert.Contains("Files modified: 1/5", summary);
    }
}
