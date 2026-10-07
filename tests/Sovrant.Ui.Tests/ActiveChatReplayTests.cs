using System.Text.Json;
using Sovrant.Desktop.ViewModels;
using Sovrant.Runtime.Conversation;
using Sovrant.Web.Services;
using Xunit;

namespace Sovrant.Ui.Tests;

/// <summary>
/// Leaving a chat while a reply is running and coming back must show the whole reply. The replay
/// buffer used to stop at 500 events — a long reply streams thousands of text chunks — so coming
/// back lost the middle of it. Text chunks are now merged; other events are kept in order.
/// </summary>
public sealed class ActiveChatReplayTests
{
    private static readonly string[] Words = Enumerable.Range(0, 2000).Select(i => $"w{i} ").ToArray();
    private static readonly string FullReply = string.Concat(Words);

    [Fact]
    public void The_Buffer_Merges_Text_And_Keeps_Other_Events_In_Order()
    {
        var buffer = new List<object>();
        RuntimeEventBuffer.Add(buffer, new RuntimeEvent.TextChunk("Hel"));
        RuntimeEventBuffer.Add(buffer, new RuntimeEvent.TextChunk("lo"));
        RuntimeEventBuffer.Add(buffer, new RuntimeEvent.ToolUseRequested("t1", "WebSearch", JsonDocument.Parse("{}").RootElement));
        RuntimeEventBuffer.Add(buffer, new RuntimeEvent.TextChunk(" again"));

        Assert.Equal(3, buffer.Count);
        Assert.Equal("Hello", ((RuntimeEvent.TextChunk)buffer[0]).Text);
        Assert.IsType<RuntimeEvent.ToolUseRequested>(buffer[1]);
        Assert.Equal(" again", ((RuntimeEvent.TextChunk)buffer[2]).Text);
    }

    [Fact]
    public void Web_Replays_A_Long_Reply_In_Full()
    {
        using var sessions = new ActiveSessionsService();
        using var cts = new CancellationTokenSource();
        Assert.True(sessions.TryRegister("s1", "long reply", cts));
        foreach (var w in Words) sessions.PushEvent("s1", new RuntimeEvent.TextChunk(w));

        var (buffered, _) = sessions.Attach("s1", _ => { });
        Assert.Equal(FullReply, string.Concat(buffered.OfType<RuntimeEvent.TextChunk>().Select(t => t.Text)));
    }

    [Fact]
    public void Desktop_Replays_A_Long_Reply_In_Full()
    {
        var sessions = new ActiveSessionsViewModel();
        using var cts = new CancellationTokenSource();
        sessions.BeginTurn("s1", "long reply", cts);
        foreach (var w in Words) sessions.PushEvent("s1", new RuntimeEvent.TextChunk(w));

        var (buffered, _) = sessions.Attach("s1", _ => { });
        Assert.Equal(FullReply, string.Concat(buffered.OfType<RuntimeEvent.TextChunk>().Select(t => t.Text)));
    }

    [Fact]
    public void A_Finished_Reply_Is_Not_Running_So_Reopening_Loads_It_Instead_Of_Replaying()
    {
        // It stays in the Active list (ticked) after finishing; reopening it must load the saved
        // conversation, not replay the reply on top of it (that showed it twice).
        using var sessions = new ActiveSessionsService();
        using var cts = new CancellationTokenSource();
        Assert.True(sessions.TryRegister("s1", "are you here", cts));
        sessions.PushEvent("s1", new RuntimeEvent.TextChunk("Yes, I'm here."));
        Assert.True(sessions.IsRunning("s1"));

        sessions.Complete("s1");

        Assert.True(sessions.HasSession("s1"));   // still listed, ticked
        Assert.False(sessions.IsRunning("s1"));   // but not replayed
        Assert.False(sessions.IsRunning("nope"));
    }
}
