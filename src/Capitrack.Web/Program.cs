using Capitrack.Web;
using Capitrack.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// HttpClient that forwards the auth cookie (same-origin via the nginx proxy).
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddHttpClient("Capitrack", c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<CookieHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Capitrack"));

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ModalService>();
builder.Services.AddScoped<SettingsStore>();
builder.Services.AddScoped<TopBarState>();

await builder.Build().RunAsync();
