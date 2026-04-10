using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Storage;

namespace Sovrant.Commands.Tests.Commands;

public sealed class RememberCommandTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMemoryStore _store;
    private readonly RememberCommand _command;

    public RememberCommandTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMemoryStore((ISqliteConnectionFactory)_provider);
        _command = new RememberCommand(_store);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
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
