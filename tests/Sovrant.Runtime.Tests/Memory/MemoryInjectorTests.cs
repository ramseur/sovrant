using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Storage;
using Sovrant.Runtime.Workspaces;

namespace Sovrant.Runtime.Tests.Memory;

public sealed class MemoryInjectorTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMemoryStore _store;
    private readonly SqliteWorkspaceStore _workspaceStore;
    private readonly MemoryInjector _injector;

    private readonly string _testUserId = "memory-test-user";

    public MemoryInjectorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMemoryStore((ISqliteConnectionFactory)_provider);
        _workspaceStore = new SqliteWorkspaceStore((ISqliteConnectionFactory)_provider);
        _injector = new MemoryInjector(_store, NullLogger<MemoryInjector>.Instance, _workspaceStore);

        // Seed test user and personal workspace.
        using var conn = ((ISqliteConnectionFactory)_provider).CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO users (user_id, username, role, status, created_at, updated_at)
            VALUES ($id, $id, 'user', 'active',
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
                    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
            """;
        cmd.Parameters.AddWithValue("$id", _testUserId);
        cmd.ExecuteNonQuery();
        _workspaceStore.CreatePersonalWorkspaceAsync(_testUserId).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task BuildMemorySection_NoMemories_ReturnsEmpty()
    {
        var result = await _injector.BuildMemorySectionAsync("/nonexistent");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesSummaries()
    {
        await _store.SaveSummaryAsync(new SessionSummary
        {
            SessionId = "s1",
            Project = "/proj",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["Fix the auth bug"],
            ToolsUsed = ["bash", "edit_file"],
            Outcome = SessionOutcome.Success,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Recent sessions", result);
        Assert.Contains("Fix the auth bug", result);
        Assert.Contains("bash", result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesPatterns()
    {
        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "This project uses xUnit",
            Project = "/proj",
            Confidence = 0.9,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Learned patterns", result);
        Assert.Contains("This project uses xUnit", result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesInstincts()
    {
        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "user mentions testing",
            Action = "Check test framework first",
            Confidence = 0.8,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Behavioral instincts", result);
        Assert.Contains("user mentions testing", result);
        Assert.Contains("Check test framework first", result);
    }

    [Fact]
    public async Task BuildMemorySection_ExcludesLowConfidenceInstincts()
    {
        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "low confidence trigger",
            Action = "Should not appear",
            Confidence = 0.35, // Below the 0.4 threshold in MemoryInjector
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.DoesNotContain("low confidence trigger", result);
    }

    [Fact]
    public async Task BuildMemorySection_ShowsConfidenceForLowPatterns()
    {
        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "Uncertain pattern",
            Project = "/proj",
            Confidence = 0.5,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("confidence: 0.5", result);
    }

    [Fact]
    public async Task BuildMemorySection_AllThreeLayers()
    {
        await _store.SaveSummaryAsync(new SessionSummary
        {
            SessionId = "s1",
            Project = "/proj",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAt = DateTimeOffset.UtcNow,
            Tasks = ["Task one"],
            ToolsUsed = ["bash"],
            Outcome = SessionOutcome.Success,
        });

        await _store.SavePatternAsync(new LearnedPattern
        {
            Id = "p1",
            Pattern = "Uses NUnit",
            Project = "/proj",
            Confidence = 0.9,
        });

        await _store.SaveInstinctAsync(new Instinct
        {
            Id = "i1",
            Trigger = "test gen",
            Action = "Use NUnit",
            Confidence = 0.8,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.Contains("Recent sessions", result);
        Assert.Contains("Learned patterns", result);
        Assert.Contains("Behavioral instincts", result);
    }

    [Fact]
    public async Task BuildMemorySection_IncludesWorkspaceMemory_FilteredByProject()
    {
        var ws = await _workspaceStore.GetPersonalAsync(_testUserId);
        Assert.NotNull(ws);

        // Workspace-wide entry — always visible.
        await _workspaceStore.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "m-wide",
            WorkspaceId = ws.WorkspaceId,
            Layer = "pattern",
            Content = "always run dotnet format before commit",
            ProjectId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        // Project-scoped entry for the active project.
        await _workspaceStore.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "m-active",
            WorkspaceId = ws.WorkspaceId,
            Layer = "instinct",
            Content = "skip integration tests in CI for active project",
            ProjectId = "proj-active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        // Project-scoped entry for a different project — must be filtered out.
        await _workspaceStore.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "m-other",
            WorkspaceId = ws.WorkspaceId,
            Layer = "pattern",
            Content = "should not appear for active project",
            ProjectId = "proj-other",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj", ws.WorkspaceId, "proj-active");

        Assert.Contains("Workspace memory (user-saved)", result);
        Assert.Contains("always run dotnet format", result);
        Assert.Contains("skip integration tests in CI for active project", result);
        Assert.DoesNotContain("should not appear for active project", result);
    }

    [Fact]
    public async Task BuildMemorySection_OmitsWorkspaceSection_WhenNoWorkspaceId()
    {
        var ws = await _workspaceStore.GetPersonalAsync(_testUserId);
        Assert.NotNull(ws);
        await _workspaceStore.SaveMemoryAsync(new WorkspaceMemoryEntry
        {
            MemoryId = "m1",
            WorkspaceId = ws.WorkspaceId,
            Layer = "pattern",
            Content = "should not surface without workspace context",
            ProjectId = null,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var result = await _injector.BuildMemorySectionAsync("/proj");

        Assert.DoesNotContain("Workspace memory (user-saved)", result);
        Assert.DoesNotContain("should not surface without workspace context", result);
    }
}
