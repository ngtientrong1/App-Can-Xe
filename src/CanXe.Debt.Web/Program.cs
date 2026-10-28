using System.Security.Claims;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using CanXe.Debt.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

// Dates are written dd/MM/yyyy everywhere; the "/" in a format string is the culture's own separator, so pin the culture.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

var dataDir = DebtPaths.ResolveDataDirectory(builder.Configuration);
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "debt.db");

// Keep the sign-in keys on disk so people stay signed in across restarts; on Windows they are sealed to this machine.
var keys = builder.Services.AddDataProtection()
    .SetApplicationName("CanXeDebt")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));
if (OperatingSystem.IsWindows())
    keys.ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddDbContext<DebtDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<TicketAdjustService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddSingleton<LoginThrottle>();
builder.Services.AddSingleton(new SetupCodeStore(dataDir));
builder.Services.AddSingleton(new BackupManager(dbPath, Path.Combine(dataDir, "backup")));
builder.Services.AddHostedService<DailyBackupService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Denied";
        o.Cookie.Name = "canxe-debt";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = async context =>
        {
            // A disabled or removed account stops working immediately, not when its cookie expires.
            var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = context.HttpContext.RequestServices.GetRequiredService<DebtDbContext>();
            var active = int.TryParse(idText, out var id)
                         && await users.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive);
            if (!active)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("Admin", p => p.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Setup");
    o.Conventions.AllowAnonymousToPage("/Denied");
    o.Conventions.AllowAnonymousToPage("/Error");
    o.Conventions.AuthorizeFolder("/Admin", "Admin");
}).AddMvcOptions(o => o.ModelBinderProviders.Insert(0, new DateOnlyModelBinderProvider()));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

var errorLog = new FileErrorLog(Path.Combine(dataDir, "logs", "error.log"));

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DebtDbContext>();

    // A database made by an older version gets the newer columns and tables, after a safety backup of it.
    var pending = await SchemaUpgrader.PendingChangesAsync(db);
    if (pending.Count > 0)
    {
        var backup = app.Services.GetRequiredService<BackupManager>().Create(BackupManager.BeforeUpgrade);
        if (!backup.Ok)
            throw new InvalidOperationException($"Không sao lưu được dữ liệu trước khi nâng cấp nên chưa nâng cấp: {backup.Error}");

        await SchemaUpgrader.ApplyAsync(db);
        app.Logger.LogWarning("Đã nâng cấp cơ sở dữ liệu ({Changes}). Bản sao lưu trước khi nâng cấp: {File}", string.Join(", ", pending), backup.Info!.FileName);
    }

    db.Database.EnsureCreated();

    var purged = await ImportService.PurgeServiceWeighsAsync(db);
    if (purged > 0)
        app.Logger.LogWarning("Đã bỏ {Count} phiếu Cân Dịch Vụ ra khỏi công nợ.", purged);
    var codes = app.Services.GetRequiredService<SetupCodeStore>();
    if (!await db.Users.AnyAsync())
    {
        var code = codes.EnsureCode();
        app.Logger.LogWarning("Chưa có tài khoản. Mở trang /Setup và nhập mã cài đặt: {Code} (cũng lưu trong {Path})", code, codes.FilePath);
    }
    else
    {
        codes.Clear();
    }
}
catch (Exception ex)
{
    errorLog.Write("Khởi động thất bại", ex);
    throw;
}
app.UseForwardedHeaders();

// The app is reached over plain http only on this machine. Any other host name comes through the https proxy
// (Tailscale), so treat it as https: cookies get the Secure flag and browsers are told to stay on https.
app.Use(async (context, next) =>
{
    if (!Security.IsLocalHost(context.Request.Host.Host))
    {
        context.Request.Scheme = "https";
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
    }

    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// Inside the exception handler, so every unhandled error is written to logs\error.log before the error page shows.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        errorLog.Write($"{context.Request.Method} {context.Request.Path}{context.Request.QueryString}", ex);
        throw;
    }
});

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'";
    headers["Cache-Control"] = "no-store";
    await next();
});

app.UseStaticFiles();
app.UseAuthentication();

// An account that must set up two-step sign-in can only reach the page for that until it has.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (context.User.HasClaim(Security.NeedsTwoStepClaim, "1")
        && !path.StartsWithSegments("/Account") && !path.StartsWithSegments("/Logout")
        && !path.StartsWithSegments("/css") && !path.StartsWithSegments("/js") && !path.StartsWithSegments("/Error"))
    {
        context.Response.Redirect("/Account?setup=1");
        return;
    }

    await next();
});
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public partial class Program;

