namespace Client.Tests;

/// <summary>While data loads, screens show the shared loading indicator, never a placeholder number.</summary>
public class LoadingStateTests : TestContext
{
    [Fact]
    public void LoadingState_is_announced_to_screen_readers()
    {
        var cut = RenderComponent<LoadingState>(p => p.Add(x => x.Text, "Loading holdings…"));
        cut.Find("[role=status]").TextContent.Should().Contain("Loading holdings…");
    }

    [Fact]
    public void A_loading_stat_card_shows_a_skeleton_instead_of_its_value()
    {
        var cut = RenderComponent<StatCard>(p => p.Add(x => x.Label, "Invested").Add(x => x.Value, "€0.00").Add(x => x.Delta, "+0%").Add(x => x.Loading, true));
        cut.Find(".skeleton").Should().NotBeNull();
        cut.Markup.Should().NotContain("€0.00").And.NotContain("+0%");

        cut.SetParametersAndRender(p => p.Add(x => x.Loading, false));
        cut.Markup.Should().Contain("€0.00");
        cut.FindAll(".skeleton").Should().BeEmpty();
    }

    [Fact]
    public void A_loading_chart_shows_the_loading_indicator_and_keeps_its_period_selector()
    {
        var cut = RenderComponent<PeriodChart>(p => p.Add(x => x.Data, [1.0, 2.0]).Add(x => x.Loading, true));
        cut.Find("[role=status]").TextContent.Should().Contain("Loading chart");
        cut.FindAll(".pill-toggle button").Should().HaveCount(8);
    }
}
