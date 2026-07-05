using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Server.Api.Auth;
using Server.Api.Middleware;
using Server.Api.Services;
using Server.Application;
using Server.Application.Common.Interfaces;
using Server.Infrastructure;
using Server.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---- Layers ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// ---- JSON: snake_case property names (matches the API contract) ----
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Capitrack API",
        Version = "v1",
        Description = "Personal wealth tracking and investment portfolio management API."
    });
});

// ---- Auth: cookie, 401 (not 302) for unauthenticated API calls ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "capitrack.sid";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Fallback only — each sign-in stamps the ticket with the user's configured lifetime
        // (15–120 min), and OnValidatePrincipal below re-applies it when the setting changes.
        o.ExpireTimeSpan = TimeSpan.FromMinutes(Server.Domain.Users.User.MaxSessionLifetimeMinutes);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
        // Enforce the CURRENT configured session lifetime on every request, so changing the
        // setting also applies to cookies issued earlier (shorter → sessions past the new
        // limit are rejected; different → the ticket window is rewritten and the cookie renewed).
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var username = ctx.Principal?.Identity?.Name;
            if (string.IsNullOrEmpty(username) || ctx.Properties.IssuedUtc is not { } issued) return;

            var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByUsernameAsync(username, ctx.HttpContext.RequestAborted);
            if (user is null)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var lifetime = TimeSpan.FromMinutes(user.SessionLifetimeMinutes);
            if (DateTimeOffset.UtcNow > issued + lifetime)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            if (ctx.Properties.ExpiresUtc - issued != lifetime)
            {
                ctx.Properties.ExpiresUtc = issued + lifetime;
                ctx.ShouldRenew = true;
            }
        };
    });
builder.Services.AddAuthorization();

// Persist DataProtection keys so the auth cookie survives restarts.
var dbPath = DbPathResolver.Resolve();
var dataDir = Path.GetDirectoryName(dbPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir ?? ".", "dp-keys")));

// Prune old sign-in audit rows on a schedule so the attempts table can't grow unbounded.
builder.Services.AddHostedService<LoginAttemptRetentionService>();

// Behind nginx — honour X-Forwarded-* headers, but ONLY when the immediate peer is a private-network
// proxy. Trusting every proxy (KnownIPNetworks.Clear()) would let anyone who could reach the API port
// directly forge X-Forwarded-For to spoof the client IP the blacklist/rate-limiter keys on. Restricting
// trust to the private ranges (where the nginx sidecar lives) keeps the real client IP correct behind
// the proxy while ignoring a forged header from any public direct connection.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.ForwardLimit = null; // walk the whole chain, popping trusted (private) hops until the real client IP
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.IPv6Loopback, 128));
    o.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("fc00::"), 7)); // IPv6 unique-local
});

// Optional CORS for separate-origin dev.
var corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS");
if (!string.IsNullOrEmpty(corsOrigins))
{
    builder.Services.AddCors(o => o.AddPolicy("dev", p =>
        p.WithOrigins(corsOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
         .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

var app = builder.Build();

// ---- Initialise database + seed on first run ----
Server.Infrastructure.DependencyInjection.InitializeDatabase(app.Services);

app.UseForwardedHeaders();
if (!string.IsNullOrEmpty(corsOrigins)) app.UseCors("dev");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
