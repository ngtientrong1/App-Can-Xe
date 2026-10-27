using System.Security.Claims;
using CanXe.Debt.Data;
using CanXe.Debt.Services;
using CanXe.Debt.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

var dataDir = DebtPaths.ResolveDataDirectory(builder.Configuration);
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "debt.db");

builder.Services.AddDbContext<DebtDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<PaymentService>();
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
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DebtDbContext>();
    db.Database.EnsureCreated();
    var codes = app.Services.GetRequiredService<SetupCodeStore>();
    if (!await db.Users.AnyAsync())
    {
        var code = codes.EnsureCode();
        app.Logger.LogWarning("ChÆ°a cÃ³ tÃ i khoáº£n. Má»Ÿ trang /Setup vÃ  nháº­p mÃ£ cÃ i Ä‘áº·t: {Code} (cÅ©ng lÆ°u trong {Path})", code, codes.FilePath);
    }
    else
    {
        codes.Clear();
    }
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

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
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public partial class Program;

