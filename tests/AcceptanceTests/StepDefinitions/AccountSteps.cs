using AcceptanceTests.Support;
using FluentAssertions;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class AccountSteps(CapitrackDriver app)
{
    [When(@"an account is created with the following details:")]
    public async Task WhenAnAccountIsCreated(DataTable details)
    {
        var account = details.CreateInstance<AccountDetails>();
        await app.CreateAccountAsync(account.Name);
    }

    [When(@"an account named ""(.*)"" is created")]
    public Task WhenAnAccountNamedIsCreated(string name) => app.CreateAccountAsync(name);

    [Then(@"the account ""(.*)"" is listed under Accounts")]
    public async Task ThenTheAccountIsListed(string name) =>
        (await app.AccountIsListedAsync(name)).Should().BeTrue($"the account '{name}' should be listed under Accounts");

    /// <summary>Row shape for the account-creation data table.</summary>
    private sealed class AccountDetails
    {
        public string Name { get; init; } = "";
    }
}
