using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Microsoft.Playwright;

namespace AcceptanceTests.Support;

/// <summary>
/// Boots the whole Capitrack stack for end-to-end tests: runs the api + web
/// images on a shared network (nginx proxies /api to the api container) and
/// launches a headless browser pointed at the running web app.
///
/// In CI the images are pre-built with BuildKit and passed in via the
/// CAPITRACK_API_IMAGE / CAPITRACK_WEB_IMAGE environment variables (fast and
/// memory-friendly). Locally, with those unset, the images are built from the
/// repo's Dockerfiles on demand.
/// </summary>
public sealed class AppEnvironment : IAsyncDisposable
{
    private INetwork _network = default!;
    private IContainer _api = default!;
    private IContainer _web = default!;

    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public string BaseUrl { get; private set; } = "";

    public static async Task<AppEnvironment> StartAsync()
    {
        var env = new AppEnvironment();

        var apiImage = await ResolveImageAsync("CAPITRACK_API_IMAGE", "capitrack-acc-api:latest", "docker/api.Dockerfile");
        var webImage = await ResolveImageAsync("CAPITRACK_WEB_IMAGE", "capitrack-acc-web:latest", "docker/web.Dockerfile");

        env._network = new NetworkBuilder().Build();

        env._api = new ContainerBuilder()
            .WithImage(apiImage)
            .WithNetwork(env._network)
            .WithNetworkAliases("api")
            .WithEnvironment("CAPITRACK_INIT_USERNAME", "admin")
            .WithEnvironment("CAPITRACK_INIT_PASSWORD", "admin123")
            .WithEnvironment("CAPITRACK_BASE_CURRENCY", "EUR")
            .WithEnvironment("DB_PATH", "/app/data/capitrack.db")
            .WithExposedPort(8080)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(8080)))
            .Build();
        await env._api.StartAsync();

        env._web = new ContainerBuilder()
            .WithImage(webImage)
            .WithNetwork(env._network)
            .WithNetworkAliases("web")
            .WithExposedPort(80)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/").ForPort(80)))
            .Build();
        await env._web.StartAsync();

        env.BaseUrl = $"http://{env._web.Hostname}:{env._web.GetMappedPublicPort(80)}";

        env.Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        env.Browser = await env.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        return env;
    }

    private static async Task<string> ResolveImageAsync(string envVar, string localTag, string dockerfile)
    {
        var prebuilt = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(prebuilt)) return prebuilt;

        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
            .WithDockerfile(dockerfile)
            .WithName(localTag)
            .WithCleanUp(false)
            .Build();
        await image.CreateAsync();
        return localTag;
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        if (_web is not null) await _web.DisposeAsync();
        if (_api is not null) await _api.DisposeAsync();
        if (_network is not null) await _network.DisposeAsync();
    }
}
