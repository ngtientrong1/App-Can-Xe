using System.Globalization;

namespace CanXe.Domain.Services;

public static class RtspUrlBuilder
{
    public const int DefaultPort = 554;
    public const string DefaultTransport = "TCP";

    public static string Build(
        string? host,
        int? port,
        string? path,
        string? username = null,
        string? password = null,
        string? transport = null)
    {
        var normalizedHost = host?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedHost))
            return string.Empty;

        var normalizedPath = NormalizePath(path);
        var effectivePort = port is > 0 ? port.Value : DefaultPort;
        var credentials = BuildCredentials(username, password);
        var url = $"rtsp://{credentials}{normalizedHost}:{effectivePort}{normalizedPath}";

        if (!string.IsNullOrWhiteSpace(transport)
            && !string.Equals(transport.Trim(), DefaultTransport, StringComparison.OrdinalIgnoreCase))
            url += $"?transport={Uri.EscapeDataString(transport.Trim())}";

        return url;
    }

    public static bool TryParse(string? rtspUrl, out RtspEndpoint endpoint)
    {
        endpoint = default;
        if (string.IsNullOrWhiteSpace(rtspUrl))
            return false;

        if (!Uri.TryCreate(rtspUrl.Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
            return false;

        endpoint = new RtspEndpoint
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : DefaultPort,
            Path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath,
            Username = string.IsNullOrEmpty(uri.UserInfo)
                ? null
                : uri.UserInfo.Split(':', 2)[0],
            Transport = ParseTransport(uri.Query)
        };
        return !string.IsNullOrWhiteSpace(endpoint.Host);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        var trimmed = path.Trim();
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }

    private static string BuildCredentials(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
            return string.Empty;

        var user = Uri.EscapeDataString(username.Trim());
        if (string.IsNullOrEmpty(password))
            return $"{user}@";

        return $"{user}:{Uri.EscapeDataString(password)}@";
    }

    private static string? ParseTransport(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return DefaultTransport;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2
                && string.Equals(kv[0], "transport", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }

        return DefaultTransport;
    }
}

public readonly record struct RtspEndpoint
{
    public string Host { get; init; }
    public int Port { get; init; }
    public string Path { get; init; }
    public string? Username { get; init; }
    public string Transport { get; init; }
}
