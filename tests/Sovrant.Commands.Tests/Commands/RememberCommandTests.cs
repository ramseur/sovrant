using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Memory;

namespace Sovrant.Commands.Tests.Commands;

public sealed class RememberCommandTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileMemoryStore _store;
    private readonly RememberCommand _command;

    public RememberCommandTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store = new FileMemoryStore(
            Path.Combine(_tempDir, "summaries"),
            Path.Combine(_tempDir, "learned"),
            Path.Combine(_tempDir, "instincts"),
            NullLogger<FileMemoryStore>.Instance);

        _command = new RememberCommand(_store);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public void Name_IsRemember() => Assert.Equal("remember", _command.Name);

    [Fact]
    public async Task EmptyArgs_ShowsUsage()
    {
        var result = await _command.ExecuteAsync("");
        Assert.Contains("Usage", result.Output);
    }

    [Fact]
    public async Task SavePattern_DefaultPrefix()
    {
        var result = await _command.ExecuteAsync("This project uses NUnit");

        Assert.Contains("Saved pattern", result.Output);

        var project = Directory.GetCurrentDirectory();
        var patterns = await _store.LoadPatternsAsync(project);
        Assert.Single(patterns);
        Assert.Equal("This project uses NUnit", patterns[0].Pattern);
        Assert.Equal(0.7, patterns[0].Confidence); // User-provided starts at 0.7
    }

    [Fact]
    public async Task SavePattern_ExplicitPrefix()
    {
        var result = await _command.ExecuteAsync("pattern API routes follow REST");

        Assert.Contains("Saved pattern", result.Output);

        var project = Directory.GetCurrentDirectory();
        var patterns = await _store.LoadPatternsAsync(project);
        Assert.Single(patterns);
        Assert.Equal("API routes follow REST", patterns[0].Pattern);
    }

    [Fact]
    public async Task SaveInstinct_ValidFormat()
    {
        var result = await _command.ExecuteAsync("instinct user corrects test framework | Check dependencies first");

        Assert.Contains("Saved instinct", result.Output);

        var instincts = await _store.LoadInstinctsAsync(0.0);
        Assert.Single(instincts);
        Assert.Equal("user corrects test framework", instincts[0].Trigger);
        Assert.Equal("Check dependencies first", instincts[0].Action);
    }

    [Fact]
    public async Task SaveInstinct_MissingPipe_ShowsError()
    {
        var result = await _command.ExecuteAsync("instinct no pipe here");
        Assert.Contains("format", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
