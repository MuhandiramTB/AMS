using System.Security.Cryptography;
using TAMS.Application.Common.Ports;

namespace TAMS.Infrastructure.Security;

/// <summary>
/// PBKDF2 (SHA-256) password hasher with per-user salt and a tuned iteration
/// count. Format: {iterations}.{saltB64}.{hashB64}. Verification is constant-time.
/// (06 §4, FR-AUTH-004.) A memory-hard algorithm (Argon2) can replace this behind
/// the same port without touching the domain.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210_000; // OWASP-recommended baseline for PBKDF2-SHA256
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    // A fixed, valid dummy hash (of a random throwaway password) used only to
    // spend comparable CPU time on the unknown-user path. (06 §4.2.)
    private static readonly string DummyHash =
        $"{Iterations}.{Convert.ToBase64String(new byte[SaltSize])}.{Convert.ToBase64String(new byte[KeySize])}";

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public void VerifyDummy(string password)
    {
        // Discard the result — this exists purely to consume comparable time.
        _ = Verify(password, DummyHash);
    }
}
