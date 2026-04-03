using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Session;

namespace Sovrant.Runtime.Tests.Session;

/// <summary>Tests for <see cref="JsonlSessionStore"/>.</summary>
public sealed class JsonlSessionStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}");
    private readonly JsonlSessionStore _store;

    public JsonlSessionStoreTests()
    {
        _store = new JsonlSessionStore(_tempDir, NullLogger<JsonlSessionStore>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task AppendAsync_And_LoadAsync_RoundTrip()
    {
        var entry = new SessionEntry("id1", DateTimeOffset.UtcNow, "user", "Hello");
        await _store.AppendAsync("session1", entry);

        var loaded = await _store.LoadAsync("session1");
        Assert.Single(loaded);
        Assert.Equal("id1", loaded[0].Id);
        Assert.Equal("user", loaded[0].Role);
        Assert.Equal("Hello", loaded[0].Content);
    }

    [Fact]
    public async Task AppendAsync_MultipleEntries_LoadsInOrder()
    {
        for (var i = 0; i < 5; i++)
        {
            await _store.AppendAsync("session2",
                new SessionEntry($"id{i}", DateTimeOffset.UtcNow, "user", $"msg{i}"));
        }

        var loaded = await _store.LoadAsync("session2");
        Assert.Equal(5, loaded.Count);
        for (var i = 0; i < 5; i++)
            Assert.Equal($"msg{i}", loaded[i].Content);
    }

    [Fact]
    public async Task LoadAsync_NonExistentSession_ReturnsEmpty()
    {
        var loaded = await _store.LoadAsync("does_not_exist");
        Assert.Empty(loaded);
    }

    [Fact]
    public async Task ListAsync_ReturnsSessionIds()
    {
        await _store.AppendAsync("sess_a", new SessionEntry("x", DateTimeOffset.UtcNow, "user", "a"));
        await _store.AppendAsync("sess_b", new SessionEntry("y", DateTimeOffset.UtcNow, "user", "b"));

        var ids = await _store.ListAsync();
        Assert.Contains("sess_a", ids);
        Assert.Contains("sess_b", ids);
    }

    [Fact]
    public async Task RotatesFile_WhenExceedsThreshold()
    {
        // Fill the file to exceed the 256KB threshold by appending many entries
        var largeContent = new string('x', 4096); // 4KB per entry
        for (var i = 0; i < 70; i++) // 70 * 4KB = ~280KB
        {
            await _store.AppendAsync("session_rotate",
                new SessionEntry($"id{i}", DateTimeOffset.UtcNow, "user", largeContent));
        }

        // After rotation, there should be a .1.jsonl file
        var rotation = Path.Combine(_tempDir, "session_rotate.1.jsonl");
        Assert.True(File.Exists(rotation));
    }
}
