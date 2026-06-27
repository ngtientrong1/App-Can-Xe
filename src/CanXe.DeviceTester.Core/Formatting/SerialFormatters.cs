namespace CanXe.DeviceTester.Core.Formatting;

public static class SerialHexFormatter
{
    public static string ToHexString(ReadOnlySpan<byte> bytes) =>
        bytes.Length == 0
            ? string.Empty
            : string.Join(' ', bytes.ToArray().Select(b => b.ToString("X2")));
}

public static class SerialTextEscaper
{
    public static string Escape(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var builder = new System.Text.StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            switch (b)
            {
                case 0x0D: builder.Append("\\r"); break;
                case 0x0A: builder.Append("\\n"); break;
                case 0x00: builder.Append("\\0"); break;
                case >= 0x20 and <= 0x7E: builder.Append((char)b); break;
                default: builder.Append("\\x").Append(b.ToString("X2")); break;
            }
        }

        return builder.ToString();
    }
}

public static class SerialEncodingHelper
{
    public static System.Text.Encoding GetEncoding(Models.SerialTextEncodingOption option) => option switch
    {
        Models.SerialTextEncodingOption.Utf8 => System.Text.Encoding.UTF8,
        Models.SerialTextEncodingOption.Windows1258 => System.Text.Encoding.GetEncoding(1258),
        _ => System.Text.Encoding.ASCII
    };
}
