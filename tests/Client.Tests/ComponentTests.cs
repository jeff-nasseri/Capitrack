namespace Client.Tests;

public class ComponentTests : TestContext
{
    [Theory]
    [InlineData("buy", "Buy")]
    [InlineData("sell", "Sell")]
    [InlineData("dividend", "Dividend")]
    [InlineData("transfer_in", "Transfer In")]
    [InlineData("fee", "Fee")]
    public void TxnBadge_renders_friendly_label(string type, string expected)
    {
        var cut = RenderComponent<TxnBadge>(p => p.Add(x => x.Type, type));
        cut.Find("span.chip").TextContent.Trim().Should().Be(expected);
    }

    [Fact]
    public void TxnBadge_unknown_type_falls_back_to_raw_value()
    {
        var cut = RenderComponent<TxnBadge>(p => p.Add(x => x.Type, "custom"));
        cut.Find("span.chip").TextContent.Trim().Should().Be("custom");
    }

    [Fact]
    public void Coin_renders_a_coin_chip()
    {
        var cut = RenderComponent<Coin>(p => p.Add(x => x.Sym, "BTC-USD"));
        cut.Find("div.coin").Should().NotBeNull();
        cut.Markup.Should().Contain("linear-gradient");
    }

    [Fact]
    public void Coin_small_size_adds_modifier_class()
    {
        var cut = RenderComponent<Coin>(p => p.Add(x => x.Sym, "AAPL").Add(x => x.Size, "sm"));
        cut.Markup.Should().Contain("coin-sm");
    }

    [Theory]
    [InlineData(0, "0%")]
    [InlineData(75, "75%")]
    [InlineData(33.4, "33%")]
    [InlineData(100, "100%")]
    public void Ring_shows_rounded_percentage(double pct, string expected)
    {
        var cut = RenderComponent<Ring>(p => p.Add(x => x.Pct, pct));
        cut.Markup.Should().Contain(expected);
    }

    [Fact]
    public void Icon_renders_an_svg()
    {
        var cut = RenderComponent<Icon>(p => p.Add(x => x.Name, "search"));
        cut.Find("svg").Should().NotBeNull();
    }

    [Fact]
    public void StatCard_renders_label_and_value()
    {
        var cut = RenderComponent<StatCard>(p => p
            .Add(x => x.Label, "Invested")
            .Add(x => x.Value, "12,345"));
        cut.Markup.Should().Contain("Invested");
        cut.Markup.Should().Contain("12,345");
    }
}
