using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Tests.Artifacts;

public sealed class LocalArtifactStoreTests : IDisposable
{
    private readonly string _root;
    private readonly LocalArtifactStore _store;

    public LocalArtifactStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sovrant_artifacts_{Guid.NewGuid():N}");
        _store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static ArtifactScope MakeScope(string? runId = null) => new()
    {
        WorkspaceId = "ws-1",
        ProjectId = "proj-1",
        RunId = runId ?? $"run-{Guid.NewGuid():N}",
    };

    // ── CreateRunScope ──────────────────────────────────────────────────

    [Fact]
    public async Task CreateRunScope_CreatesDirectoryAndManifest()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        Assert.True(Directory.Exists(handle.Location));
        Assert.True(File.Exists(Path.Combine(handle.Location, "_manifest.json")));
    }

    [Fact]
    public async Task CreateRunScope_RequiresRunId()
    {
        var scope = new ArtifactScope { RunId = null };
        await Assert.ThrowsAsync<ArgumentException>(() => _store.CreateRunScopeAsync(scope));
    }

    // ── Write + Read roundtrip ──────────────────────────────────────────

    [Fact]
    public async Task Write_And_Read_RoundTrips()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        var content = "Hello, artifacts!"u8.ToArray();
        using var writeStream = new MemoryStream(content);
        await _store.WriteAsync(handle, "test.txt", writeStream);

        using var readStream = await _store.ReadAsync(handle, "test.txt");
        using var reader = new StreamReader(readStream);
        var result = await reader.ReadToEndAsync();

        Assert.Equal("Hello, artifacts!", result);
    }

    [Fact]
    public async Task Write_NestedPath_CreatesSubdirectories()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        using var stream = new MemoryStream("nested"u8.ToArray());
        await _store.WriteAsync(handle, "sub/dir/file.txt", stream);

        Assert.True(File.Exists(Path.Combine(handle.Location, "sub", "dir", "file.txt")));
    }

    // ── Path traversal rejection ────────────────────────────────────────

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\etc\\passwd")]
    [InlineData("sub/../../../escape.txt")]
    public async Task Write_RejectsPathTraversal(string maliciousPath)
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        using var stream = new MemoryStream("evil"u8.ToArray());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.WriteAsync(handle, maliciousPath, stream));
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\etc\\passwd")]
    public async Task Read_RejectsPathTraversal(string maliciousPath)
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.ReadAsync(handle, maliciousPath));
    }

    [Fact]
    public async Task CreateRunScope_RejectsTraversalInSegments()
    {
        var scope = new ArtifactScope
        {
            WorkspaceId = "../escape",
            ProjectId = "proj-1",
            RunId = "run-1",
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _store.CreateRunScopeAsync(scope));
    }

    // ── Read not found ──────────────────────────────────────────────────

    [Fact]
    public async Task Read_ThrowsWhenNotFound()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _store.ReadAsync(handle, "nonexistent.txt"));
    }

    // ── ListAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsFiles_ExcludesManifest()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        using var s1 = new MemoryStream("a"u8.ToArray());
        await _store.WriteAsync(handle, "file1.txt", s1);

        using var s2 = new MemoryStream("b"u8.ToArray());
        await _store.WriteAsync(handle, "file2.md", s2);

        var entries = new List<ArtifactEntry>();
        await foreach (var e in _store.ListAsync(scope))
            entries.Add(e);

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, e => e.RelativePath.Contains("_manifest", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListAsync_AtProjectLevel_ListsAcrossRuns()
    {
        var run1 = MakeScope("run-1");
        var run2 = MakeScope("run-2");

        var h1 = await _store.CreateRunScopeAsync(run1);
        var h2 = await _store.CreateRunScopeAsync(run2);

        using var s1 = new MemoryStream("a"u8.ToArray());
        await _store.WriteAsync(h1, "a.txt", s1);

        using var s2 = new MemoryStream("b"u8.ToArray());
        await _store.WriteAsync(h2, "b.txt", s2);

        // List at project level (no RunId)
        var projectScope = new ArtifactScope
        {
            WorkspaceId = "ws-1",
            ProjectId = "proj-1",
            RunId = null,
        };

        var entries = new List<ArtifactEntry>();
        await foreach (var e in _store.ListAsync(projectScope))
            entries.Add(e);

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task ListAsync_EmptyScope_ReturnsEmpty()
    {
        var scope = MakeScope("nonexistent-run");
        var entries = new List<ArtifactEntry>();
        await foreach (var e in _store.ListAsync(scope))
            entries.Add(e);

        Assert.Empty(entries);
    }

    // ── DeleteAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesRunDirectory()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        using var stream = new MemoryStream("content"u8.ToArray());
        await _store.WriteAsync(handle, "file.txt", stream);

        await _store.DeleteAsync(scope);

        Assert.False(Directory.Exists(handle.Location));
    }

    [Fact]
    public async Task DeleteAsync_NonexistentScope_DoesNotThrow()
    {
        var scope = MakeScope("nonexistent");
        await _store.DeleteAsync(scope); // should not throw
    }

    // ── GetAccessUrlAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetAccessUrl_ReturnsFileUri()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        using var stream = new MemoryStream("content"u8.ToArray());
        await _store.WriteAsync(handle, "output.md", stream);

        var url = await _store.GetAccessUrlAsync(handle, "output.md", TimeSpan.FromMinutes(5));

        Assert.NotNull(url);
        Assert.StartsWith("file:///", url.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAccessUrl_ReturnsNull_WhenMissing()
    {
        var scope = MakeScope();
        var handle = await _store.CreateRunScopeAsync(scope);

        var url = await _store.GetAccessUrlAsync(handle, "missing.txt", TimeSpan.FromMinutes(5));
        Assert.Null(url);
    }

    // ── Rerun isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task TwoRuns_SamePrompt_ProduceDistinctDirectories()
    {
        var scope1 = MakeScope("run-1");
        var scope2 = MakeScope("run-2");

        var h1 = await _store.CreateRunScopeAsync(scope1);
        var h2 = await _store.CreateRunScopeAsync(scope2);

        Assert.NotEqual(h1.Location, h2.Location);

        using var s1 = new MemoryStream("first"u8.ToArray());
        await _store.WriteAsync(h1, "output.txt", s1);

        using var s2 = new MemoryStream("second"u8.ToArray());
        await _store.WriteAsync(h2, "output.txt", s2);

        // Both files exist independently
        using var r1 = await _store.ReadAsync(h1, "output.txt");
        using var r2 = await _store.ReadAsync(h2, "output.txt");

        using var reader1 = new StreamReader(r1);
        using var reader2 = new StreamReader(r2);

        Assert.Equal("first", await reader1.ReadToEndAsync());
        Assert.Equal("second", await reader2.ReadToEndAsync());
    }

    // ── Fallback scope ──────────────────────────────────────────────────

    [Fact]
    public async Task FallbackScope_UsesDefaults_AndSucceeds()
    {
        var scope = new ArtifactScope { RunId = "run-fallback" };

        var handle = await _store.CreateRunScopeAsync(scope);
        Assert.Contains("ws-personal-", handle.Location, StringComparison.Ordinal);
        // Projects-only layout: default project is a real project folder, not a bypass —
        // {root}/{ws}/projects/default-project/artifacts/{run}
        Assert.Equal("default-project", handle.Scope.ProjectId);
        Assert.Contains(Path.DirectorySeparatorChar + "projects" + Path.DirectorySeparatorChar, handle.Location, StringComparison.Ordinal);
        Assert.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar, handle.Location, StringComparison.Ordinal);
        // No user segment in the path
        Assert.DoesNotContain("default-user", handle.Location, StringComparison.Ordinal);

        using var stream = new MemoryStream("works"u8.ToArray());
        await _store.WriteAsync(handle, "test.txt", stream);

        using var readStream = await _store.ReadAsync(handle, "test.txt");
        using var reader = new StreamReader(readStream);
        Assert.Equal("works", await reader.ReadToEndAsync());
    }

    // ── Workspace sharing ───────────────────────────────────────────────

    [Fact]
    public async Task SameWorkspace_DifferentUsers_ShareArtifacts()
    {
        // Two users write to the same workspace — artifacts are shared
        var aliceScope = new ArtifactScope
        {
            WorkspaceId = "shared-ws",
            ProjectId = "proj-1",
            RunId = "run-alice",
            UserId = "alice",
        };
        var bobScope = new ArtifactScope
        {
            WorkspaceId = "shared-ws",
            ProjectId = "proj-1",
            RunId = "run-bob",
            UserId = "bob",
        };

        var h1 = await _store.CreateRunScopeAsync(aliceScope);
        var h2 = await _store.CreateRunScopeAsync(bobScope);

        using var s1 = new MemoryStream("alice's work"u8.ToArray());
        await _store.WriteAsync(h1, "report.md", s1);

        using var s2 = new MemoryStream("bob's work"u8.ToArray());
        await _store.WriteAsync(h2, "analysis.md", s2);

        // List at project level — both users' runs are visible
        var projectScope = new ArtifactScope
        {
            WorkspaceId = "shared-ws",
            ProjectId = "proj-1",
        };

        var entries = new List<ArtifactEntry>();
        await foreach (var e in _store.ListAsync(projectScope))
            entries.Add(e);

        Assert.Equal(2, entries.Count);
    }
}
