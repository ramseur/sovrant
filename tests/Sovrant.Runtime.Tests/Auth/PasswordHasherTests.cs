using Sovrant.Runtime.Auth;

namespace Sovrant.Runtime.Tests.Auth;

public sealed class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new Argon2idPasswordHasher();

    [Fact]
    public void Hash_ProducesSaltAndHashFormat()
    {
        var hash = _hasher.Hash("correct-horse-battery");
        Assert.Contains("|", hash, StringComparison.Ordinal);
        var parts = hash.Split('|');
        Assert.Equal(2, parts.Length);
        Assert.True(parts[0].Length > 0);
        Assert.True(parts[1].Length > 0);
    }

    [Fact]
    public void Verify_ReturnsTrueForCorrectPassword()
    {
        var hash = _hasher.Hash("correct-horse-battery");
        Assert.True(_hasher.Verify("correct-horse-battery", hash));
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongPassword()
    {
        var hash = _hasher.Hash("correct-horse-battery");
        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Hash_ProducesDifferentHashesForSamePassword()
    {
        var h1 = _hasher.Hash("same-password");
        var h2 = _hasher.Hash("same-password");
        Assert.NotEqual(h1, h2);
    }
}
