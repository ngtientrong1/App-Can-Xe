using System.Security.Cryptography;

namespace CanXe.Debt.Services;

public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    public const int MinimumLength = 8;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record OperationResult(bool Ok, string? Error = null)
{
    public static OperationResult Success { get; } = new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

public sealed record OperationResult<T>(bool Ok, T? Value = default, string? Error = null)
{
    public static OperationResult<T> Success(T value) => new(true, value);
    public static OperationResult<T> Fail(string error) => new(false, default, error);
}
