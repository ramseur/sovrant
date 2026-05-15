using Sovrant.Runtime.Mcp;

namespace Sovrant.Runtime.Tests.Mcp;

/// <summary>Tests for <see cref="AesGcmCredentialStore"/>.</summary>
public sealed class AesGcmCredentialStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"sovrant-cred-tests-{Guid.NewGuid():N}");

    private AesGcmCredentialStore CreateStore() => new(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task StoreAsync_ThenRetrieveAsync_ReturnsOriginalValue()
    {
        var store = CreateStore();
        await store.StoreAsync("mykey", "my-secret-value");
        var result = await store.RetrieveAsync("mykey");
        Assert.Equal("my-secret-value", result);
    }

    [Fact]
    public async Task RetrieveAsync_UnknownKey_ReturnsNull()
    {
        var store = CreateStore();
        var result = await store.RetrieveAsync("does-not-exist");
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAsync_OverwritesExistingKey()
    {
        var store = CreateStore();
        await store.StoreAsync("key", "first");
        await store.StoreAsync("key", "second");
        var result = await store.RetrieveAsync("key");
        Assert.Equal("second", result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesStoredCredential()
    {
        var store = CreateStore();
        await store.StoreAsync("key", "value");
        await store.DeleteAsync("key");
        var result = await store.RetrieveAsync("key");
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_DoesNotThrow()
    {
        var store = CreateStore();
        await store.DeleteAsync("missing-key"); // should not throw
    }

    [Fact]
    public async Task StoreAsync_CreatesCredentialsDirectory()
    {
        Assert.False(Directory.Exists(_tempDir));
        var store = CreateStore();
        await store.StoreAsync("key", "value");
        Assert.True(Directory.Exists(_tempDir));
    }

    [Fact]
    public async Task StoreAsync_CreatesKeystoreFile()
    {
        var store = CreateStore();
        await store.StoreAsync("key", "value");
        var keystorePath = Path.Combine(_tempDir, ".keystore");
        Assert.True(File.Exists(keystorePath));
    }

    [Fact]
    public async Task TwoStores_SameDirectory_ShareMasterKey()
    {
        // First store writes the credential.
        var store1 = CreateStore();
        await store1.StoreAsync("shared-key", "shared-value");

        // Second store (same dir) should be able to decrypt it.
        var store2 = CreateStore();
        var result = await store2.RetrieveAsync("shared-key");
        Assert.Equal("shared-value", result);
    }

    [Fact]
    public async Task StoreAsync_DifferentKeys_StoredIndependently()
    {
        var store = CreateStore();
        await store.StoreAsync("alpha", "alpha-val");
        await store.StoreAsync("beta", "beta-val");

        Assert.Equal("alpha-val", await store.RetrieveAsync("alpha"));
        Assert.Equal("beta-val", await store.RetrieveAsync("beta"));
    }

    [Fact]
    public async Task RetrieveAsync_CorruptBlob_ReturnsNull()
    {
        var store = CreateStore();
        // Ensure directory and keystore are initialised.
        await store.StoreAsync("seed", "seed");

        // Write a corrupt blob directly.
        var key = "corrupt-key";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(key)));
        var path = Path.Combine(_tempDir, $"{hash}.enc");
        await File.WriteAllBytesAsync(path, new byte[30]); // all zeros — invalid tag

        var result = await store.RetrieveAsync(key);
        Assert.Null(result);
    }
}
