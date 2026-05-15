using System.Text.Json;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Runtime.Tests.Conversation;

public sealed class RuntimeEventDtoTests
{
    [Fact]
    public void TextChunk_RoundTrips()
    {
        var original = new RuntimeEvent.TextChunk("Hello world");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("text_chunk", dto.Type);
        Assert.Equal("Hello world", dto.Text);

        var back = dto.ToEvent();
        var tc = Assert.IsType<RuntimeEvent.TextChunk>(back);
        Assert.Equal("Hello world", tc.Text);
    }

    [Fact]
    public void ToolUseRequested_RoundTrips()
    {
        var input = JsonDocument.Parse("""{"path": "/tmp"}""").RootElement;
        var original = new RuntimeEvent.ToolUseRequested("tu1", "read_file", input);
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("tool_use_requested", dto.Type);
        Assert.Equal("tu1", dto.ToolUseId);
        Assert.Equal("read_file", dto.ToolName);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.ToolUseRequested>(back);
        Assert.Equal("tu1", ev.ToolUseId);
    }

    [Fact]
    public void ToolResult_RoundTrips()
    {
        var original = new RuntimeEvent.ToolResult("tu1", "bash", "output", false);
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("tool_result", dto.Type);
        Assert.Equal("output", dto.Content);
        Assert.False(dto.IsError);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.ToolResult>(back);
        Assert.Equal("output", ev.Content);
    }

    [Fact]
    public void TurnComplete_RoundTrips()
    {
        var original = new RuntimeEvent.TurnComplete("end_turn", 100, 50);
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("turn_complete", dto.Type);
        Assert.Equal(100, dto.InputTokens);
        Assert.Equal(50, dto.OutputTokens);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.TurnComplete>(back);
        Assert.Equal(100, ev.InputTokens);
    }

    [Fact]
    public void TurnCost_RoundTrips()
    {
        var original = new RuntimeEvent.TurnCost(0.0025m, "openrouter");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("turn_cost", dto.Type);
        Assert.Equal(0.0025m, dto.EstimatedUsd);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.TurnCost>(back);
        Assert.Equal(0.0025m, ev.EstimatedUsd);
    }

    [Fact]
    public void ClarificationNeeded_RoundTrips()
    {
        var original = new RuntimeEvent.ClarificationNeeded("What file?");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("clarification_needed", dto.Type);
        Assert.Equal("What file?", dto.Question);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.ClarificationNeeded>(back);
        Assert.Equal("What file?", ev.Question);
    }

    [Fact]
    public void PlanPresented_RoundTrips()
    {
        var original = new RuntimeEvent.PlanPresented("p1", "Step 1: Do X\nStep 2: Do Y", true);
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("plan_presented", dto.Type);
        Assert.Equal("p1", dto.PlanId);
        Assert.True(dto.RequiresApproval);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.PlanPresented>(back);
        Assert.Equal("p1", ev.PlanId);
    }

    [Fact]
    public void StepProgress_RoundTrips()
    {
        var original = new RuntimeEvent.StepProgress(2, 5, "refactor", "in_progress");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("step_progress", dto.Type);
        Assert.Equal(2, dto.Current);
        Assert.Equal(5, dto.Total);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.StepProgress>(back);
        Assert.Equal(2, ev.Current);
    }

    [Fact]
    public void RuntimeError_RoundTrips()
    {
        var original = new RuntimeEvent.RuntimeError("Something broke");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("runtime_error", dto.Type);
        Assert.Equal("Something broke", dto.Message);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.RuntimeError>(back);
        Assert.Equal("Something broke", ev.Message);
    }

    [Fact]
    public void PermissionDenied_RoundTrips()
    {
        var original = new RuntimeEvent.PermissionDenied("bash", "Not allowed");
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("permission_denied", dto.Type);
        Assert.Equal("bash", dto.ToolName);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.PermissionDenied>(back);
        Assert.Equal("Not allowed", ev.Reason);
    }

    [Fact]
    public void AuthRequired_RoundTrips()
    {
        var original = new RuntimeEvent.AuthRequired("github-mcp", new Uri("https://github.com/login/oauth"));
        var dto = RuntimeEventDto.FromEvent(original);
        Assert.Equal("auth_required", dto.Type);
        Assert.Equal("github-mcp", dto.ServerName);
        Assert.Contains("github.com", dto.AuthorizationUrl, StringComparison.Ordinal);

        var back = dto.ToEvent();
        var ev = Assert.IsType<RuntimeEvent.AuthRequired>(back);
        Assert.Equal("github-mcp", ev.ServerName);
    }

    [Fact]
    public void UnknownEvent_ReturnsNull()
    {
        var dto = new RuntimeEventDto { Type = "some_future_event" };
        Assert.Null(dto.ToEvent());
    }

    [Fact]
    public void Dto_SerializesClean()
    {
        var dto = RuntimeEventDto.FromEvent(new RuntimeEvent.TextChunk("hi"));
        var json = JsonSerializer.Serialize(dto);
        Assert.Contains("\"type\":\"text_chunk\"", json, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"hi\"", json, StringComparison.Ordinal);
        // Null fields should be omitted.
        Assert.DoesNotContain("tool_use_id", json, StringComparison.Ordinal);
    }
}
