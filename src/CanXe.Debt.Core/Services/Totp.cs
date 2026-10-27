using System.Security.Cryptography;
using System.Text;

namespace CanXe.Debt.Services;

/// <summary>Time-based one-time codes (RFC 6238, 6 digits, 30 s) as used by Google Authenticator and similar apps.</summary>
public static class Totp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    public const int StepSeconds = 30;
    public const int Digits = 6;

    public static string GenerateSecret() => Base32Encode(RandomNumberGenerator.GetBytes(20));

    public static long StepOf(DateTimeOffset time) => time.ToUnixTimeSeconds() / StepSeconds;

    public static string ComputeCode(string base32Secret, long step)
    {
        var key = Base32Decode(base32Secret);
        var counter = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Checks a typed code against the current step and one step either side (clock drift). A step that was already used
    /// (<paramref name="lastUsedStep"/>) or older is refused, so a code seen over a shoulder cannot be replayed.
    /// </summary>
    public static bool Verify(string base32Secret, string? code, DateTimeOffset now, long lastUsedStep, out long matchedStep)
    {
        matchedStep = 0;
        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != Digits)
            return false;

        var current = StepOf(now);
        for (var step = current - 1; step <= current + 1; step++)
        {
            if (step <= lastUsedStep)
                continue;
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ComputeCode(base32Secret, step)), Encoding.ASCII.GetBytes(digits)))
            {
                matchedStep = step;
                return true;
            }
        }

        return false;
    }

    /// <summary>The otpauth:// address the authenticator app reads from the QR code.</summary>
    public static string BuildUri(string issuer, string account, string base32Secret) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}?secret={base32Secret}&issuer={Uri.EscapeDataString(issuer)}&digits={Digits}&period={StepSeconds}";

    public static string FormatSecret(string base32Secret) =>
        string.Join(' ', Enumerable.Range(0, (base32Secret.Length + 3) / 4).Select(i => base32Secret.Substring(i * 4, Math.Min(4, base32Secret.Length - i * 4))));

    public static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
            sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] Base32Decode(string text)
    {
        var clean = text.Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>(clean.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (var ch in clean)
        {
            var value = Alphabet.IndexOf(ch);
            if (value < 0)
                throw new FormatException("Mã không đúng định dạng.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}
