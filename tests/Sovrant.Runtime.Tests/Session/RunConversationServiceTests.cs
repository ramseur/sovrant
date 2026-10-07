using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Session;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Session;

/// <summary>Phase 133 — a run started outside chat gets its own conversation.</summary>
public sealed class RunConversationServiceTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ISessionStore _sessions;
    private readonly RunConversationService _service;

    public RunConversationServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _sessions = new SqliteSessionStore(_provider);
        _service = new RunConversationService(_sessions);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file still held (SQLite pool, indexer, antivirus) */ }
    }

    [Fact]
    public async Task Start_CreatesAnOwnedTitledConversationSeededWithTheGoal()
    {
        Assert.True(await _service.StartAsync("run-1", "alice", "Team run", "Review the auth module"));

        var item = (await _sessions.ListWithTitlesAsync("alice")).Single(s => s.SessionId == "run-1");
        Assert.Equal("Team run: Review the auth module", item.Title);
        var entries = await _sessions.LoadAsync("run-1", "alice");
        Assert.Equal("user", entries.Single().Role);
        Assert.Equal("Review the auth module", entries.Single().Content);
        Assert.Empty(await _sessions.ListWithTitlesAsync("bob"));
    }

    [Fact]
    public async Task Complete_AppendsTheOutcome_AndTruncatesLongOutput()
    {
        await _service.StartAsync("run-2", "alice", "Swarm", "Summarise the repo");
        await _service.CompleteAsync("run-2", "alice", "Swarm", succeeded: true, new string('x', RunConversationService.MaxOutputChars + 50), TimeSpan.FromSeconds(12.3));

        var last = (await _sessions.LoadAsync("run-2", "alice"))[^1];
        Assert.Equal("assistant", last.Role);
        Assert.StartsWith("Swarm finished in 12.3s.", last.Content, StringComparison.Ordinal);
        Assert.EndsWith("…(truncated)", last.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_ReportsFailure()
    {
        await _service.StartAsync("run-3", "alice", "Team run", "Do the thing");
        await _service.CompleteAsync("run-3", "alice", "Team run", succeeded: false, "boom");

        Assert.Equal("Team run failed.\n\nboom", (await _sessions.LoadAsync("run-3", "alice"))[^1].Content);
    }

    [Fact]
    public async Task LongGoal_GivesAShortTitle()
    {
        await _service.StartAsync("run-4", "alice", "Swarm", string.Join(' ', Enumerable.Repeat("word", 40)));
        var title = (await _sessions.ListWithTitlesAsync("alice")).Single().Title!;
        Assert.True(title.Length <= 60);
        Assert.EndsWith("...", title, StringComparison.Ordinal);
    }
}
