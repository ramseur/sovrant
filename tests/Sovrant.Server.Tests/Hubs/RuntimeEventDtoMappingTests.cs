using System.Text.Json;
using Sovrant.Runtime.Conversation;

namespace Sovrant.Server.Tests.Hubs;

/// <summary>
/// Verifies that <see cref="RuntimeEventDto"/> correctly maps all
/// <see cref="RuntimeEvent"/> subtypes for the SignalR transport.
/// </summary>
public sealed class RuntimeEventDtoMappingTests
{
    [Fact]
    public void AllEventTypes_MapToDistinctDtoTypes()
    {
        var events = new RuntimeEvent[]
        {
            new RuntimeEvent.TextChunk("hi"),
            new RuntimeEvent.ToolUseRequested("t1", "bash", default),
            new RuntimeEvent.ToolResult("t1", "bash", "ok", false),
            new RuntimeEvent.TurnComplete("end_turn", 10, 20),
            new RuntimeEvent.TurnCost(0.01m, "test"),
            new RuntimeEvent.PermissionDenied("bash", "denied"),
            new RuntimeEvent.RuntimeError("err"),
            new RuntimeEvent.AuthRequired("srv", new Uri("https://example.com")),
            new RuntimeEvent.ClarificationNeeded("what?"),
            new RuntimeEvent.PlanPresented("p1", "plan", true),
            new RuntimeEvent.PlanApproved("p1"),
            new RuntimeEvent.PlanRejected("p1", "bad"),
            new RuntimeEvent.StepProgress(1, 3, "do", "done"),
        };

        var types = events.Select(e => RuntimeEventDto.FromEvent(e).Type).ToList();

        // All should be unique.
        Assert.Equal(types.Count, types.Distinct().Count());

        // None should be "unknown".
        Assert.DoesNotContain("unknown", types);
    }

    [Fact]
    public void FromEvent_ThenToEvent_PreservesType()
    {
        var events = new RuntimeEvent[]
        {
            new RuntimeEvent.TextChunk("hello"),
            new RuntimeEvent.TurnComplete("stop", 5, 10),
            new RuntimeEvent.RuntimeError("oops"),
        };

        foreach (var ev in events)
        {
            var dto = RuntimeEventDto.FromEvent(ev);
            var back = dto.ToEvent();
            Assert.NotNull(back);
            Assert.Equal(ev.GetType(), back!.GetType());
        }
    }

    [Fact]
    public void Dto_JsonRoundTrip_Works()
    {
        var original = RuntimeEventDto.FromEvent(new RuntimeEvent.TurnCost(1.5m, "openrouter"));
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<RuntimeEventDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("turn_cost", deserialized!.Type);
        Assert.Equal(1.5m, deserialized.EstimatedUsd);
        Assert.Equal("openrouter", deserialized.Source);
    }
}
