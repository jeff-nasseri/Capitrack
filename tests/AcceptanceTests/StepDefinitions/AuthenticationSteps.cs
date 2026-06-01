using AcceptanceTests.Support;
using FluentAssertions;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class AuthenticationSteps(CapitrackDriver app)
{
    [Given(@"the Capitrack application is open")]
    public Task GivenTheApplicationIsOpen() => app.OpenAppAsync();

    [Given(@"an authenticated user is signed in")]
    public Task GivenAnAuthenticatedUserIsSignedIn() => app.SignInAsAdministratorAsync();

    [When(@"credentials ""(.*)"" / ""(.*)"" are submitted")]
    public Task WhenCredentialsAreSubmitted(string username, string password) =>
        app.SignInAsync(username, password);

    [Then(@"the portfolio dashboard is shown")]
    public async Task ThenTheDashboardIsShown() =>
        (await app.IsSignedInAsync()).Should().BeTrue("the authenticated dashboard should be displayed");

    [Then(@"access is denied and the sign-in screen remains")]
    public async Task ThenAccessIsDenied() =>
        (await app.IsOnSignInAsync()).Should().BeTrue("the user should be kept on the sign-in screen");
}
