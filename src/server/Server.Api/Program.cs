using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Server.Api.Auth;
using Server.Api.Middleware;
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
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization();

// Persist DataProtection keys so the auth cookie survives restarts.
var dbPath = DbPathResolver.Resolve();
var dataDir = Path.GetDirectoryName(dbPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir ?? ".", "dp-keys")));

// Behind nginx — honour X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
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
