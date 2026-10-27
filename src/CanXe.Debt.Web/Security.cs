using System.Security.Claims;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using QRCoder;

namespace CanXe.Debt.Web;

public static class Security
{
    public const string NeedsTwoStepClaim = "needs2fa";
    public const string PendingCookie = "canxe-debt-2fa";
    private static readonly TimeSpan PendingLifetime = TimeSpan.FromMinutes(5);

    /// <summary>The address the visitor really comes from (the proxy's X-Forwarded-For is applied before this), or null when unknown.</summary>
    public static string? ClientAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return null;
        // A request that reaches us from the proxy on this machine with no forwarded address has no usable client address.
        return System.Net.IPAddress.IsLoopback(address) ? null : address.ToString();
    }

    /// <summary>True for http://localhost and http://127.0.0.1; any other name reaches us only through the https proxy.</summary>
    public static bool IsLocalHost(string? host) =>
        host is null || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "[::1]" || host == "::1";

    public static async Task SignInAsync(HttpContext context, AppUser user, bool needsTwoStepSetup)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("display", user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (needsTwoStepSetup)
            claims.Add(new Claim(NeedsTwoStepClaim, "1"));

        await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
    }

    /// <summary>Remembers, for five minutes, that the password step passed and the code step is next.</summary>
    public static void SetPending(HttpContext context, int userId)
    {
        var token = Protector(context).Protect(userId.ToString(), PendingLifetime);
        context.Response.Cookies.Append(PendingCookie, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            MaxAge = PendingLifetime
        });
    }

    public static int? ReadPending(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(PendingCookie, out var token) || string.IsNullOrEmpty(token))
            return null;

        try
        {
            return int.TryParse(Protector(context).Unprotect(token), out var id) ? id : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    public static void ClearPending(HttpContext context) => context.Response.Cookies.Delete(PendingCookie);

    /// <summary>The QR code the authenticator app scans, as inline SVG.</summary>
    public static string QrSvg(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(5);
    }

    private static ITimeLimitedDataProtector Protector(HttpContext context) =>
        context.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("CanXe.Debt.TwoStep").ToTimeLimitedDataProtector();
}
