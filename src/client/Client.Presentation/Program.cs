using Client.Application;
using Client.Application.Store.Session;
using Client.Infrastructure.Services;
using Client.Presentation;
using Fluxor;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// HttpClient that forwards the auth cookie (same-origin via the nginx proxy).
builder.Services.AddScoped<CookieHandler>();
builder.Services.AddHttpClient("Capitrack", c => c.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<CookieHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Capitrack"));

// Single scoped ApiClient, exposed both concretely (components) and via IApiClient
// (Fluxor effects + the App.razor Unauthorized subscription) — same instance.
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<IApiClient>(sp => sp.GetRequiredService<ApiClient>());

// Transient UI services (coexist with Fluxor).
builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ModalService>();
builder.Services.AddScoped<SettingsStore>();
builder.Services.AddScoped<TopBarState>();

// Fluxor state management — scans Client.Application for features/reducers/effects.
builder.Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly));

await builder.Build().RunAsync();
