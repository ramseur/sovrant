using Microsoft.Extensions.Logging.Abstractions;
using Sovrant.Runtime.Providers;
using Sovrant.Runtime.Storage;

namespace Sovrant.Runtime.Tests.Providers;

public sealed class SqliteProviderProfileStoreTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly SqliteStorageProvider _provider;
    private readonly SqliteProviderProfileStore _store;

    public SqliteProviderProfileStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sovrant_profiles_{Guid.NewGuid():N}.db");
        _provider = new SqliteStorageProvider(NullLogger<SqliteStorageProvider>.Instance, _dbPath);
        _provider.InitializeAsync().GetAwaiter().GetResult();
        _store = new SqliteProviderProfileStore(_provider);
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static ProviderProfile Sample(
        string id = "openai-default",
        string userId = "alice",
        string? defaultModel = "gpt-5",
        int? maxTokens = 8192) =>
        new(
            ProfileId: id,
            UserId: userId,
            Name: "OpenAI",
            ProviderKind: "OpenAI",
            BaseUrl: "https://api.openai.com/v1",
            DefaultModel: defaultModel,
            MaxTokens: maxTokens,
            CredentialId: $"provider.{id}.api_key",
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Create_Then_Get_RoundTrips()
    {
        var p = Sample();
        await _store.CreateAsync(p);

        var got = await _store.GetAsync(p.ProfileId);
        Assert.NotNull(got);
        Assert.Equal(p.Name, got!.Name);
        Assert.Equal(p.ProviderKind, got.ProviderKind);
        Assert.Equal(p.BaseUrl, got.BaseUrl);
        Assert.Equal(p.DefaultModel, got.DefaultModel);
        Assert.Equal(p.MaxTokens, got.MaxTokens);
        Assert.Equal(p.CredentialId, got.CredentialId);
    }

    [Fact]
    public async Task Get_MissingProfile_ReturnsNull()
    {
        Assert.Null(await _store.GetAsync("does-not-exist"));
    }

    [Fact]
    public async Task NullableFields_RoundTripAsNull()
    {
        var p = Sample(id: "ollama-local", defaultModel: null, maxTokens: null);
        await _store.CreateAsync(p);

        var got = await _store.GetAsync(p.ProfileId);
        Assert.NotNull(got);
        Assert.Null(got!.DefaultModel);
        Assert.Null(got.MaxTokens);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnersProfiles()
    {
        await _store.CreateAsync(Sample(id: "a-1", userId: "alice"));
        await _store.CreateAsync(Sample(id: "a-2", userId: "alice"));
        await _store.CreateAsync(Sample(id: "b-1", userId: "bob"));

        var alices = await _store.ListAsync("alice");
        var bobs = await _store.ListAsync("bob");

        Assert.Equal(2, alices.Count);
        Assert.Single(bobs);
        Assert.Equal("b-1", bobs[0].ProfileId);
    }

    [Fact]
    public async Task Update_ChangesMutableFields()
    {
        var p = Sample();
        await _store.CreateAsync(p);

        var changed = p with { Name = "OpenAI (renamed)", MaxTokens = 16_384 };
        var ok = await _store.UpdateAsync(changed);

        Assert.True(ok);
        var got = await _store.GetAsync(p.ProfileId);
        Assert.Equal("OpenAI (renamed)", got!.Name);
        Assert.Equal(16_384, got.MaxTokens);
    }

    [Fact]
    public async Task Update_MissingId_ReturnsFalse()
    {
        Assert.False(await _store.UpdateAsync(Sample(id: "ghost")));
    }

    [Fact]
    public async Task Delete_RemovesRow()
    {
        var p = Sample();
        await _store.CreateAsync(p);

        Assert.True(await _store.DeleteAsync(p.ProfileId));
        Assert.Null(await _store.GetAsync(p.ProfileId));
        Assert.False(await _store.DeleteAsync(p.ProfileId)); // second delete is no-op
    }
}
