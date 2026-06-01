using AcceptanceTests.Support;
using FluentAssertions;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class NavigationSteps(CapitrackDriver app)
{
    [When(@"the (.*) section is opened")]
    public Task WhenTheSectionIsOpened(string section) => app.NavigateToAsync(section);

    [Then(@"the ""(.*)"" heading is displayed")]
    public async Task ThenTheHeadingIsDisplayed(string heading) =>
        (await app.PageShowsAsync(heading)).Should().BeTrue($"the page should display the '{heading}' heading");
}
