using Microsoft.Playwright;
using Reqnroll;
using Reqnroll.BoDi;

namespace AcceptanceTests.Support;

[Binding]
public sealed class Hooks
{
    private static AppEnvironment _app = default!;
    private readonly IObjectContainer _container;
    private IBrowserContext? _context;
    private IPage? _page;

    public Hooks(IObjectContainer container) => _container = container;

    [BeforeTestRun]
    public static async Task BeforeTestRun() => _app = await AppEnvironment.StartAsync();

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (_app is not null) await _app.DisposeAsync();
    }

    [BeforeScenario]
    public async Task BeforeScenario()
    {
        _context = await _app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
        });
        _page = await _context.NewPageAsync();
        _container.RegisterInstanceAs(_page);
        _container.RegisterInstanceAs(_app);

        // Build the driver from this scenario's page + the running app's URL and
        // register it so the (thin) step-definition classes receive it by
        // constructor injection. All Playwright work happens inside the driver.
        _container.RegisterInstanceAs(new CapitrackDriver(_page, _app.BaseUrl));
    }

    [AfterScenario]
    public async Task AfterScenario()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_context is not null) await _context.DisposeAsync();
    }
}
