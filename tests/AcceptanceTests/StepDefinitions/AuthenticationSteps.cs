using AcceptanceTests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class AuthenticationSteps(IPage page, AppEnvironment app)
{
    [Given(@"the Capitrack app is open")]
    public async Task GivenTheAppIsOpen()
    {
        await page.GotoAsync(app.BaseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("input[placeholder='Enter username']", new PageWaitForSelectorOptions { Timeout = 60000 });
    }

    [Given(@"I am signed in")]
    public async Task GivenIAmSignedIn()
    {
        await GivenTheAppIsOpen();
        await WhenISignIn("admin", "admin123");
        await page.WaitForSelectorAsync("nav.rail", new PageWaitForSelectorOptions { Timeout = 30000 });
    }

    [When(@"I sign in as ""(.*)"" with password ""(.*)""")]
    public async Task WhenISignIn(string username, string password)
    {
        await page.FillAsync("input[placeholder='Enter username']", username);
        await page.FillAsync("input[placeholder='Enter password']", password);
        await page.ClickAsync("button[type='submit']");
    }

    [Then(@"I should see the dashboard")]
    public async Task ThenIShouldSeeTheDashboard()
    {
        await page.WaitForSelectorAsync("nav.rail", new PageWaitForSelectorOptions { Timeout = 30000 });
        (await page.Locator("nav.rail").CountAsync()).Should().BeGreaterThan(0);
    }

    [Then(@"I should remain on the sign-in screen")]
    public async Task ThenIShouldRemainOnSignIn()
    {
        await page.WaitForTimeoutAsync(2500);
        (await page.Locator("nav.rail").CountAsync()).Should().Be(0);
        (await page.Locator("input[placeholder='Enter password']").CountAsync()).Should().BeGreaterThan(0);
    }
}
