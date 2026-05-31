using AcceptanceTests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class NavigationSteps(IPage page, AppEnvironment app)
{
    [When(@"I navigate to ""(.*)""")]
    public async Task WhenINavigateTo(string path)
    {
        await page.GotoAsync($"{app.BaseUrl}/{path}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("nav.rail", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(1500);
    }

    [Then(@"the page should show ""(.*)""")]
    public async Task ThenThePageShouldShow(string heading)
    {
        var locator = page.GetByText(heading, new PageGetByTextOptions { Exact = false });
        (await locator.First.CountAsync()).Should().BeGreaterThan(0, $"expected the page to contain '{heading}'");
    }
}
