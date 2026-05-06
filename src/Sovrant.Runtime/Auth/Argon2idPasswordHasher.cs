using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Sovrant.Runtime.Auth;

/// <summary>
/// Argon2id password hasher using OWASP-minimum parameters:
/// memory=65536 KiB, iterations=3, parallelism=1.
///
/// Hash format: base64(salt)|base64(hash) — both are 32 bytes.
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int Iterations = 3;
    private const int MemorySize = 65536; // KiB
    private const int DegreeOfParallelism = 1;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(Encoding.UTF8.GetBytes(password), salt);

        return $"{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        var parts = hash.Split('|');
        if (parts.Length != 2)
            return false;

        byte[] salt, storedHash;
        try
        {
            salt = Convert.FromBase64String(parts[0]);
            storedHash = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var computed = ComputeHash(Encoding.UTF8.GetBytes(password), salt);
        return CryptographicOperations.FixedTimeEquals(computed, storedHash);
    }

    private static byte[] ComputeHash(byte[] password, byte[] salt)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            Iterations = Iterations,
            MemorySize = MemorySize,
            DegreeOfParallelism = DegreeOfParallelism,
        };
        return argon2.GetBytes(HashSize);
    }
}
