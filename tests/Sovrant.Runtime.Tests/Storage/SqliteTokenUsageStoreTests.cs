using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Storage;

public sealed class SqliteTokenUsageStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly ITokenUsageStore _store;

    public SqliteTokenUsageStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_test_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteTokenUsageStore((ISqliteConnectionFactory)_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task RecordAndGetTotals_RoundTrips()
    {
        await _store.RecordAsync(new TokenUsageRecord("s1", "claude-3", 100, 50));
        await _store.RecordAsync(new TokenUsageRecord("s1", "claude-3", 200, 100));

        var totals = await _store.GetSessionTotalsAsync("s1");

        Assert.Equal(300, totals.TotalInputTokens);
        Assert.Equal(150, totals.TotalOutputTokens);
    }

    [Fact]
    public async Task GetTotals_EmptySession_ReturnsZeros()
    {
        var totals = await _store.GetSessionTotalsAsync("nonexistent");

        Assert.Equal(0, totals.TotalInputTokens);
        Assert.Equal(0, totals.TotalOutputTokens);
        Assert.Null(totals.TotalCostUsd);
    }

    [Fact]
    public async Task RecordWithCost_PreservesCost()
    {
        await _store.RecordAsync(new TokenUsageRecord("s1", "claude-3", 100, 50, CostUsd: 0.01m));

        var totals = await _store.GetSessionTotalsAsync("s1");
        Assert.NotNull(totals.TotalCostUsd);
    }
}
