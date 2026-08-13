using System.Security.Cryptography;

namespace Brasa.Modules.Identity.Domain;

/// <summary>
/// PBKDF2-HMAC-SHA256 PIN hashing (IDN-09). No new dependency — .NET's own
/// <see cref="Rfc2898DeriveBytes"/> already implements a vetted primitive,
/// the same "hand-roll it only where the standard library already gives a
/// safe building block" instinct this codebase applied to CSV parsing
/// (CAT-17) and the event dispatcher (ADR 0006) rather than pulling in a
/// library for either.
/// </summary>
internal static class PinHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    // OWASP's 2023 minimum recommendation for PBKDF2-HMAC-SHA256. Stored
    // alongside each hash (not hardcoded at verify time) so a future
    // increase never invalidates already-issued PINs.
    private const int Iterations = 210_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Hashes a PIN with a fresh random salt. Never call this twice expecting the same output.</summary>
    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, Algorithm, HashSizeBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Verifies a PIN against a hash produced by <see cref="Hash"/>. Constant-time comparison.</summary>
    public static bool Verify(string pin, string encodedHash)
    {
        var parts = encodedHash.Split('.');
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

        var actual = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, Algorithm, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
