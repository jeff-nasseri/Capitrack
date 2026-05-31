using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Microsoft.Playwright;

namespace AcceptanceTests.Support;

/// <summary>
/// Boots the whole Capitrack stack for end-to-end tests: builds the api + web
/// Docker images from the repo's Dockerfiles, runs them on a shared network
/// (nginx proxies /api to the api container), and launches a headless browser
/// pointed at the running web app.
/// </summary>
public sealed class AppEnvironment : IAsyncDisposable
{
    private INetwork _network = default!;
    private IFutureDockerImage _apiImage = default!;
    private IFutureDockerImage _webImage = default!;
    private IContainer _api = default!;
    private IContainer _web = default!;

    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;
    public string BaseUrl { get; private set; } = "";

    public static async Task<AppEnvironment> StartAsync()
    {
        var env = new AppEnvironment();
        var solution = CommonDirectoryPath.GetSolutionDirectory();

        env._apiImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solution, string.Empty)
            .WithDockerfile("docker/api.Dockerfile")
            .WithName("capitrack-acc-api:latest")
            .WithCleanUp(false)
            .Build();

        env._webImage = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solution, string.Empty)
            .WithDockerfile("docker/web.Dockerfile")
            .WithName("capitrack-acc-web:latest")
            .WithCleanUp(false)
            .Build();

        await env._apiImage.CreateAsync();
        await env._webImage.CreateAsync();

        env._network = new NetworkBuilder().Build();

        env._api = new ContainerBuilder()
            .WithImage(env._apiImage)
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
            .WithImage(env._webImage)
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

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        Playwright?.Dispose();
        if (_web is not null) await _web.DisposeAsync();
        if (_api is not null) await _api.DisposeAsync();
        if (_network is not null) await _network.DisposeAsync();
    }
}
