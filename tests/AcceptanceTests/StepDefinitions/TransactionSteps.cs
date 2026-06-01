using AcceptanceTests.Support;
using FluentAssertions;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class TransactionSteps(CapitrackDriver app)
{
    [Given(@"the Activity section is open")]
    public Task GivenTheActivitySectionIsOpen() => app.OpenActivityAsync();

    [When(@"the activity is filtered by the ""(.*)"" transaction type")]
    public Task WhenTheActivityIsFilteredByType(string type) => app.FilterActivityByTypeAsync(type);

    [Then(@"the transaction count is presented")]
    public async Task ThenTheTransactionCountIsPresented() =>
        (await app.ActivityCountIsShownAsync()).Should().BeTrue("the Activity toolbar should present the transaction count");
}
