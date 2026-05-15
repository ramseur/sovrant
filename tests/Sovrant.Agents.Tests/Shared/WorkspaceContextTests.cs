using Sovrant.Agents.Shared;

namespace Sovrant.Agents.Tests.Shared;

public class WorkspaceContextTests : IDisposable
{
    private readonly string _tempDir;

    public WorkspaceContextTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"workspace-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ArtifactsRoot_DefaultsToArtifactsSubdir()
    {
        var ctx = new WorkspaceContext { WorkingDirectory = _tempDir };
        Assert.Equal(Path.Combine(_tempDir, "artifacts"), ctx.ArtifactsRoot);
    }

    [Fact]
    public async Task GetOrCreateArtifactsDirectory_CreatesFolder()
    {
        var ctx = new WorkspaceContext { WorkingDirectory = _tempDir };
        var dir = await ctx.GetOrCreateArtifactsDirectoryAsync("Write a whitepaper on lean knitting");

        Assert.True(Directory.Exists(dir));
        Assert.StartsWith(Path.Combine(_tempDir, "artifacts"), dir);
        // "write" and "a" are stop words, so slug should contain key terms
        Assert.Contains("whitepaper", dir);
        Assert.Contains("lean-knitting", dir);
    }

    [Fact]
    public async Task GetOrCreateArtifactsDirectory_SamePromptReturnsSamePath()
    {
        var ctx = new WorkspaceContext { WorkingDirectory = _tempDir };
        var dir1 = await ctx.GetOrCreateArtifactsDirectoryAsync("Create a report");
        var dir2 = await ctx.GetOrCreateArtifactsDirectoryAsync("Create a report");

        Assert.Equal(dir1, dir2);
    }

    [Theory]
    [InlineData("Hello World!", "hello-world")]
    [InlineData("analyze the data", "analyze-data")]
    [InlineData("", "output")]
    [InlineData("   ", "output")]
    [InlineData("!!!???", "output")]
    public void Slugify_ProducesExpectedOutput(string input, string expected)
    {
        Assert.Equal(expected, WorkspaceContext.Slugify(input));
    }

    [Fact]
    public void Slugify_StripsStopWords()
    {
        // "create a guide for the features" -> strips create, a, for, the
        var slug = WorkspaceContext.Slugify("create a guide for the features");
        Assert.Equal("guide-features", slug);
    }

    [Fact]
    public void Slugify_TruncatesLongPrompts()
    {
        var longPrompt = "implement comprehensive observability monitoring alerting dashboarding metrics tracing logging correlation analysis visualization";
        var slug = WorkspaceContext.Slugify(longPrompt);

        Assert.True(slug.Length <= 40, $"Slug too long ({slug.Length}): {slug}");
    }

    [Fact]
    public void Slugify_UserPromptExample()
    {
        // The prompt that generated the too-long slug
        var slug = WorkspaceContext.Slugify("create a guide for a list of features users demand in an ai platform in md form with explainations of why this is a good feature");
        Assert.True(slug.Length <= 40, $"Slug too long ({slug.Length}): {slug}");
        Assert.Contains("features", slug);
    }

    [Fact]
    public void Slugify_CrossPlatformSafe()
    {
        var slug = WorkspaceContext.Slugify("path\\to/file:name<with>special|chars");

        Assert.DoesNotContain("\\", slug);
        Assert.DoesNotContain("/", slug);
        Assert.DoesNotContain(":", slug);
        Assert.DoesNotContain("<", slug);
        Assert.DoesNotContain(">", slug);
        Assert.DoesNotContain("|", slug);
    }
}
