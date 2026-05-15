using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Commands.Commands;
using Sovrant.Runtime.Memory;
using Sovrant.Runtime.Storage;

namespace Sovrant.Commands.Tests.Commands;

public sealed class ForgetCommandTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteMemoryStore _store;
    private readonly ForgetCommand _command;

    public ForgetCommandTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteMemoryStore((ISqliteConnectionFactory)_provider);
        _command = new ForgetCommand(_store);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public void Name_IsForget() => Assert.Equal("forget", _command.Name);

    [Fact]
    public async Task EmptyArgs_ShowsUsage()
    {
        var result = await _command.ExecuteAsync("");
        Assert.Contains("Usage", result.Output);
    }

    [Fact]
    public async Task List_NoMemories_ShowsEmpty()
    {
        var result = await _command.ExecuteAsync("list");
        Assert.Contains("No patterns or instincts", result.Output);
    }

    [Fact]
    public async Task List_ShowsPatternsAndInstincts()
    {
        var project = Directory.GetCurrentDirectory();
        await _store.SavePatternAsync(new LearnedPattern { Id = "p1", Pattern = "Uses xUnit", Project = project, Confidence = 0.9 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "test", Action = "check deps", Confidence = 0.7 });

        var result = await _command.ExecuteAsync("list");

        Assert.Contains("p1", result.Output);
        Assert.Contains("Uses xUnit", result.Output);
        Assert.Contains("i1", result.Output);
        Assert.Contains("test", result.Output);
    }

    [Fact]
    public async Task ForgetPattern_RemovesIt()
    {
        var project = Directory.GetCurrentDirectory();
        await _store.SavePatternAsync(new LearnedPattern { Id = "p1", Pattern = "Gone", Project = project });

        var result = await _command.ExecuteAsync("pattern p1");
        Assert.Contains("Removed pattern", result.Output);

        var patterns = await _store.LoadPatternsAsync(project);
        Assert.Empty(patterns);
    }

    [Fact]
    public async Task ForgetInstinct_RemovesIt()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B" });

        var result = await _command.ExecuteAsync("instinct i1");
        Assert.Contains("Removed instinct", result.Output);

        var instincts = await _store.LoadInstinctsAsync(0.0);
        Assert.Empty(instincts);
    }

    [Fact]
    public async Task Prune_RemovesLowConfidence()
    {
        await _store.SaveInstinctAsync(new Instinct { Id = "i1", Trigger = "A", Action = "B", Confidence = 0.1 });
        await _store.SaveInstinctAsync(new Instinct { Id = "i2", Trigger = "C", Action = "D", Confidence = 0.8 });

        var result = await _command.ExecuteAsync("prune");
        Assert.Contains("Pruned 1", result.Output);
    }

    [Fact]
    public async Task Prune_NothingToPrune()
    {
        var result = await _command.ExecuteAsync("prune");
        Assert.Contains("No instincts to prune", result.Output);
    }

    [Fact]
    public async Task UnknownSubcommand_ShowsError()
    {
        var result = await _command.ExecuteAsync("banana");
        Assert.Contains("Unknown subcommand", result.Output);
    }
}
