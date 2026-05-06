namespace Sovrant.Runtime.Auth;

/// <summary>Hashes and verifies passwords using Argon2id.</summary>
public interface IPasswordHasher
{
    /// <summary>Returns an Argon2id PHC-format hash of <paramref name="password"/>.</summary>
    string Hash(string password);

    /// <summary>Returns true when <paramref name="password"/> matches <paramref name="hash"/>.</summary>
    bool Verify(string password, string hash);
}
