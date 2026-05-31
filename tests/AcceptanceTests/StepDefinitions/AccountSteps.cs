using AcceptanceTests.Support;
using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class AccountSteps(IPage page, AppEnvironment app)
{
    [When(@"I create an account named ""(.*)""")]
    public async Task WhenICreateAnAccount(string name)
    {
        await page.GotoAsync($"{app.BaseUrl}/accounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("nav.rail", new PageWaitForSelectorOptions { Timeout = 30000 });
        await page.WaitForTimeoutAsync(1000);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add account" }).First.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        // The account form's first text input is the name.
        var nameInput = page.Locator("input.input[type='text'], input.input:not([type])").First;
        await nameInput.FillAsync(name);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).First.ClickAsync();
        await page.WaitForTimeoutAsync(1800);
    }

    [Then(@"the account ""(.*)"" should appear in my accounts")]
    public async Task ThenTheAccountShouldAppear(string name)
    {
        await page.GotoAsync($"{app.BaseUrl}/accounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(1500);
        (await page.GetByText(name).First.CountAsync()).Should().BeGreaterThan(0);
    }
}
