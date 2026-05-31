using System.Text.Json;
using Capitrack.Api.Data;
using Capitrack.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Database path + data directory ----
var dbPath = DbPathResolver.Resolve();
var dataDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);

builder.Services.AddDbContext<CapitrackDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// ---- JSON: snake_case property names (matches the original API contract) ----
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    // Dictionary keys (e.g. the /quotes map keyed by symbol) are left untouched.
});

// ---- Auth: cookie, 401 (not 302) for unauthenticated API calls ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "capitrack.sid";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Persist DataProtection keys so the auth cookie survives container restarts.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir ?? ".", "dp-keys")));

// Behind nginx — honour X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// Optional CORS for separate-origin dev (set CORS_ORIGINS="http://localhost:5000,...").
var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS");
if (!string.IsNullOrEmpty(corsOrigins))
{
    builder.Services.AddCors(o => o.AddPolicy("dev", p =>
        p.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

// ---- Services ----
builder.Services.AddSingleton<YahooFinanceClient>();
builder.Services.AddScoped<PriceService>();
builder.Services.AddScoped<WealthService>();
builder.Services.AddScoped<ImporterService>();

var app = builder.Build();

// ---- Initialise database + seed on first run ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CapitrackDbContext>();
    db.Database.EnsureCreated();
    SeedService.Seed(db);
}

app.UseForwardedHeaders();
if (!string.IsNullOrEmpty(corsOrigins)) app.UseCors("dev");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
