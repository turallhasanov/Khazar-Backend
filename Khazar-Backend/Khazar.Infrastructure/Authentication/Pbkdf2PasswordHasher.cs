using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Khazar.Application.Common.Interfaces;

namespace Khazar.Infrastructure.Authentication;

/// <summary>
/// PBKDF2-HMAC-SHA512 password hashing (OWASP: 210,000 iterations). The stored value is
/// "PBKDF2-SHA512$iterations$salt$hash", so the iteration count can be raised later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2-SHA512";
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashBytes);

        return string.Join('$', Prefix, Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (password is null || string.IsNullOrEmpty(passwordHash))
            return false;

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length == 0 || expected.Length == 0)
                return false;

            var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
