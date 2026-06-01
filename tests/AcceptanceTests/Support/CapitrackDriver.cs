using Microsoft.Playwright;

namespace AcceptanceTests.Support;

/// <summary>
/// The single point of contact between the step definitions and the Capitrack
/// web application. Every Playwright interaction lives here, behind
/// intention-revealing methods, so that the step definitions read as plain
/// business language and stay free of UI mechanics (selectors, waits, timeouts).
///
/// One driver instance is created per scenario from that scenario's
/// <see cref="IPage"/> and the running app's base URL (see
/// <c>Support/Hooks.cs</c>), then injected into the step classes via the
/// Reqnroll object container.
/// </summary>
public sealed class CapitrackDriver(IPage page, string baseUrl)
{
    private const int NavTimeoutMs = 30_000;
    private const int SignInTimeoutMs = 60_000;

    // The sign-in form fields, the submit control and the post-login chrome.
    private const string UsernameField = "input[placeholder='Enter username']";
    private const string PasswordField = "input[placeholder='Enter password']";
    private const string SubmitButton = "button[type='submit']";
    private const string NavigationRail = "nav.rail";

    /// <summary>Opens the application and waits for the sign-in form to render.</summary>
    public async Task OpenAppAsync()
    {
        await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync(UsernameField, new PageWaitForSelectorOptions { Timeout = SignInTimeoutMs });
    }

    /// <summary>Submits the given credentials through the sign-in form.</summary>
    public async Task SignInAsync(string username, string password)
    {
        await page.FillAsync(UsernameField, username);
        await page.FillAsync(PasswordField, password);
        await page.ClickAsync(SubmitButton);
    }

    /// <summary>
    /// Opens the app and signs in with the seeded administrator credentials,
    /// waiting until the authenticated shell (the navigation rail) is present.
    /// </summary>
    public async Task SignInAsAdministratorAsync()
    {
        await OpenAppAsync();
        await SignInAsync("admin", "admin123");
        await page.WaitForSelectorAsync(NavigationRail, new PageWaitForSelectorOptions { Timeout = NavTimeoutMs });
    }

    /// <summary>True once the authenticated application shell is on screen.</summary>
    public async Task<bool> IsSignedInAsync()
    {
        await page.WaitForSelectorAsync(NavigationRail, new PageWaitForSelectorOptions { Timeout = NavTimeoutMs });
        return await page.Locator(NavigationRail).CountAsync() > 0;
    }

    /// <summary>
    /// True when the visitor is still being asked to authenticate: the app shell
    /// is absent and the password field is still on screen.
    /// </summary>
    public async Task<bool> IsOnSignInAsync()
    {
        // Give any (rejected) navigation a moment to settle before asserting.
        await page.WaitForTimeoutAsync(2_500);
        var railVisible = await page.Locator(NavigationRail).CountAsync() > 0;
        var passwordVisible = await page.Locator(PasswordField).CountAsync() > 0;
        return !railVisible && passwordVisible;
    }

    /// <summary>Navigates to one of the main sections by its route (e.g. "holdings").</summary>
    public async Task NavigateToAsync(string section)
    {
        await page.GotoAsync($"{baseUrl}/{section}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync(NavigationRail, new PageWaitForSelectorOptions { Timeout = NavTimeoutMs });
        await page.WaitForTimeoutAsync(1_500);
    }

    /// <summary>True when the supplied text is rendered anywhere on the current page.</summary>
    public async Task<bool> PageShowsAsync(string text)
    {
        var locator = page.GetByText(text, new PageGetByTextOptions { Exact = false });
        return await locator.First.CountAsync() > 0;
    }

    /// <summary>Opens the Accounts section and creates a new account with the given name.</summary>
    public async Task CreateAccountAsync(string name)
    {
        await page.GotoAsync($"{baseUrl}/accounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync(NavigationRail, new PageWaitForSelectorOptions { Timeout = NavTimeoutMs });
        await page.WaitForTimeoutAsync(1_000);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Add account" }).First.ClickAsync();
        await page.WaitForTimeoutAsync(800);

        // The account form's first text input is the account name.
        var nameInput = page.Locator("input.input[type='text'], input.input:not([type])").First;
        await nameInput.FillAsync(name);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).First.ClickAsync();
        await page.WaitForTimeoutAsync(1_800);
    }

    /// <summary>True when an account with the given name is listed on the Accounts page.</summary>
    public async Task<bool> AccountIsListedAsync(string name)
    {
        await page.GotoAsync($"{baseUrl}/accounts", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForTimeoutAsync(1_500);
        return await page.GetByText(name).First.CountAsync() > 0;
    }

    /// <summary>Opens the Activity section and waits for the transaction list to be ready.</summary>
    public async Task OpenActivityAsync()
    {
        await NavigateToAsync("activity");
        // The activity toolbar carries the running "<n> transactions" count.
        await page.GetByText("transactions").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = NavTimeoutMs });
    }

    /// <summary>Filters the Activity list by transaction type (e.g. "buy", "sell", "dividend").</summary>
    public async Task FilterActivityByTypeAsync(string type)
    {
        // The second toolbar dropdown is the type filter; its option values are
        // the raw transaction types ("buy", "sell", "dividend", "transfer_in").
        var typeFilter = page.Locator("select.input").Nth(1);
        await typeFilter.SelectOptionAsync(new SelectOptionValue { Value = type });
        await page.WaitForTimeoutAsync(800);
    }

    /// <summary>True when the running transaction count is shown in the Activity toolbar.</summary>
    public async Task<bool> ActivityCountIsShownAsync() =>
        await page.GetByText("transactions").First.CountAsync() > 0;
}
