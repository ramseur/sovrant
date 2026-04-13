using Sovrant.Server.Hubs;

namespace Sovrant.Server.Tests.Hubs;

public sealed class ChatHubTests
{
    [Fact]
    public async Task PendingConfirmations_ConfirmTool_ResolvesTrue()
    {
        var tcs = new TaskCompletionSource<bool>();
        var key = "conn1:tool1";
        ChatHub.PendingConfirmations[key] = tcs;

        // Simulate confirming.
        Assert.True(ChatHub.PendingConfirmations.TryRemove(key, out var removed));
        removed!.TrySetResult(true);

        Assert.True(await tcs.Task);
    }

    [Fact]
    public async Task PendingConfirmations_DenyTool_ResolvesFalse()
    {
        var tcs = new TaskCompletionSource<bool>();
        var key = "conn2:tool2";
        ChatHub.PendingConfirmations[key] = tcs;

        Assert.True(ChatHub.PendingConfirmations.TryRemove(key, out var removed));
        removed!.TrySetResult(false);

        Assert.False(await tcs.Task);
    }

    [Fact]
    public void PendingConfirmations_MissingKey_DoesNotThrow()
    {
        // Attempting to confirm a non-existent key should not throw.
        var removed = ChatHub.PendingConfirmations.TryRemove("nonexistent:key", out _);
        Assert.False(removed);
    }

    [Fact]
    public async Task PendingConfirmations_Cleanup_RemovesByPrefix()
    {
        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();
        ChatHub.PendingConfirmations["conn3:tool_a"] = tcs1;
        ChatHub.PendingConfirmations["conn3:tool_b"] = tcs2;
        ChatHub.PendingConfirmations["conn4:tool_c"] = new TaskCompletionSource<bool>();

        // Simulate disconnection cleanup for conn3.
        var prefix = "conn3:";
        foreach (var key in ChatHub.PendingConfirmations.Keys.ToList())
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) &&
                ChatHub.PendingConfirmations.TryRemove(key, out var tcs))
            {
                tcs.TrySetResult(false);
            }
        }

        Assert.False(await tcs1.Task);
        Assert.False(await tcs2.Task);
        // conn4 should still be there.
        Assert.True(ChatHub.PendingConfirmations.ContainsKey("conn4:tool_c"));

        // Clean up.
        ChatHub.PendingConfirmations.TryRemove("conn4:tool_c", out _);
    }
}
