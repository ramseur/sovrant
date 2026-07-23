using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Artifacts;

namespace Sovrant.Runtime.Tests.Artifacts;

public sealed class LegacyArtifactImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _artifactsRoot;
    private readonly string _legacyDir;
    private readonly string _originalDir;

    public LegacyArtifactImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant_legacy_{Guid.NewGuid():N}");
        _artifactsRoot = Path.Combine(_tempDir, "scoped");
        _legacyDir = Path.Combine(_tempDir, "working", "artifacts");
        _originalDir = Directory.GetCurrentDirectory();

        Directory.CreateDirectory(Path.Combine(_tempDir, "working"));
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // Windows may still hold file locks briefly after test completes; safe to ignore in test cleanup.
        }
    }

    [Fact]
    public async Task ImportIfNeeded_MigratesLegacySlugDirs()
    {
        // Seed legacy artifacts
        var slugDir = Path.Combine(_legacyDir, "my-test-guide");
        Directory.CreateDirectory(slugDir);
        await File.WriteAllTextAsync(Path.Combine(slugDir, "output.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(slugDir, "data.json"), "{}");

        // Set working directory so the importer finds {cwd}/artifacts
        Directory.SetCurrentDirectory(Path.Combine(_tempDir, "working"));

        var store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactsRoot);
        var importer = new LegacyArtifactImporter(store, NullLogger<LegacyArtifactImporter>.Instance);

        var count = await importer.ImportIfNeededAsync();

        Assert.Equal(1, count);

        // Verify files landed under workspace-scoped layout.
        // Projects-only: default project is a real project folder — {root}/{ws}/projects/default-project/artifacts/{run}
        var expectedDir = Path.Combine(
            _artifactsRoot,
            Sovrant.Runtime.Workspaces.WorkspaceIdentity.DefaultPersonal(),
            "projects",
            "default-project",
            "artifacts",
            "legacy-my-test-guide");
        Assert.True(Directory.Exists(expectedDir));
        Assert.True(File.Exists(Path.Combine(expectedDir, "output.md")));
        Assert.True(File.Exists(Path.Combine(expectedDir, "data.json")));

        // Verify manifest was written
        Assert.True(File.Exists(Path.Combine(expectedDir, "_manifest.json")));
        var manifestText = await File.ReadAllTextAsync(Path.Combine(expectedDir, "_manifest.json"));
        Assert.Contains("legacy", manifestText, StringComparison.Ordinal);
        Assert.Contains("my-test-guide", manifestText, StringComparison.Ordinal);

        // Verify breadcrumb
        Assert.True(File.Exists(Path.Combine(_legacyDir, "README.migrated")));
    }

    [Fact]
    public async Task ImportIfNeeded_NoLegacyDir_Returns0()
    {
        Directory.SetCurrentDirectory(Path.Combine(_tempDir, "working"));

        var store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactsRoot);
        var importer = new LegacyArtifactImporter(store, NullLogger<LegacyArtifactImporter>.Instance);

        var count = await importer.ImportIfNeededAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ImportIfNeeded_EmptyLegacyDir_Returns0()
    {
        Directory.CreateDirectory(_legacyDir);
        Directory.SetCurrentDirectory(Path.Combine(_tempDir, "working"));

        var store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactsRoot);
        var importer = new LegacyArtifactImporter(store, NullLogger<LegacyArtifactImporter>.Instance);

        var count = await importer.ImportIfNeededAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ImportIfNeeded_MultipleSlugs_MigratesAll()
    {
        var slug1 = Path.Combine(_legacyDir, "guide-one");
        var slug2 = Path.Combine(_legacyDir, "report-two");
        Directory.CreateDirectory(slug1);
        Directory.CreateDirectory(slug2);
        await File.WriteAllTextAsync(Path.Combine(slug1, "a.txt"), "a");
        await File.WriteAllTextAsync(Path.Combine(slug2, "b.txt"), "b");

        Directory.SetCurrentDirectory(Path.Combine(_tempDir, "working"));

        var store = new LocalArtifactStore(NullLogger<LocalArtifactStore>.Instance, _artifactsRoot);
        var importer = new LegacyArtifactImporter(store, NullLogger<LegacyArtifactImporter>.Instance);

        var count = await importer.ImportIfNeededAsync();
        Assert.Equal(2, count);
    }
}
