using Sovrant.Runtime.Permissions;

namespace Sovrant.Runtime.Tests.Permissions;

/// <summary>Tests for <see cref="CiPermissionPolicy"/>.</summary>
public sealed class CiPermissionPolicyTests
{
    private readonly CiPermissionPolicy _policy = new();

    [Theory]
    [InlineData("read")]
    [InlineData("read_file")]
    [InlineData("glob")]
    [InlineData("grep")]
    [InlineData("ls")]
    [InlineData("list_directory")]
    [InlineData("web_fetch")]
    [InlineData("web_search")]
    [InlineData("tool_search")]
    [InlineData("sleep")]
    [InlineData("agent")]
    [InlineData("skill")]
    public void ReadTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: false));
    }

    [Theory]
    [InlineData("write")]
    [InlineData("write_file")]
    [InlineData("edit")]
    [InlineData("edit_file")]
    [InlineData("create")]
    [InlineData("create_file")]
    [InlineData("delete_file")]
    public void FileEditTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: true));
    }

    [Theory]
    [InlineData("bash")]
    [InlineData("shell")]
    [InlineData("powershell")]
    [InlineData("run_command")]
    [InlineData("execute_command")]
    public void ShellTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: true));
    }

    [Theory]
    [InlineData("enter_worktree")]
    [InlineData("exit_worktree")]
    public void WorktreeTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: true));
    }

    [Fact]
    public void UnknownDestructiveTool_IsDenied()
    {
        Assert.Equal(PolicyDecision.Deny, _policy.Evaluate("unknown_dangerous_tool", isDestructive: true));
    }

    [Fact]
    public void UnknownNonDestructiveTool_IsAllowed()
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate("some_custom_tool", isDestructive: false));
    }

    [Theory]
    [InlineData("lsp_hover")]
    [InlineData("lsp_definition")]
    [InlineData("lsp_references")]
    [InlineData("lsp_diagnostics")]
    [InlineData("lsp_rename")]
    public void LspTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: false));
    }

    [Theory]
    [InlineData("enter_plan_mode")]
    [InlineData("exit_plan_mode")]
    public void PlanModeTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: false));
    }

    [Theory]
    [InlineData("task_create")]
    [InlineData("task_get")]
    [InlineData("task_list")]
    [InlineData("task_output")]
    [InlineData("task_stop")]
    [InlineData("task_update")]
    [InlineData("todo_write")]
    public void TaskTools_AreAllowed(string toolName)
    {
        Assert.Equal(PolicyDecision.Allow, _policy.Evaluate(toolName, isDestructive: false));
    }

    [Fact]
    public void Evaluate_NullToolName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _policy.Evaluate(null!, isDestructive: false));
    }
}
