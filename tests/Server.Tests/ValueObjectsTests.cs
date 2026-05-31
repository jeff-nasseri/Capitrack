namespace Server.Tests;

public class ValueObjectsTests
{
    [Theory]
    [InlineData("usd", "USD")]
    [InlineData(" eur ", "EUR")]
    [InlineData("Gbp", "GBP")]
    public void CurrencyCode_normalises_to_uppercase(string input, string expected) =>
        CurrencyCode.Create(input).Value.Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CurrencyCode_rejects_empty(string? input)
    {
        var act = () => CurrencyCode.Create(input);
        act.Should().Throw<InvalidCurrencyCodeException>();
    }

    [Fact]
    public void CurrencyCode_equality_is_value_based()
    {
        CurrencyCode.Create("USD").Should().Be(CurrencyCode.Create("usd"));
        (CurrencyCode.Create("USD") == CurrencyCode.Usd).Should().BeTrue();
        CurrencyCode.Create("USD").Should().NotBe(CurrencyCode.Create("EUR"));
    }

    [Theory]
    [InlineData("aapl", "AAPL")]
    [InlineData("btc-usd", "BTC-USD")]
    public void Symbol_normalises_to_uppercase(string input, string expected) =>
        Symbol.Create(input).Value.Should().Be(expected);

    [Fact]
    public void Symbol_rejects_empty()
    {
        var act = () => Symbol.Create("  ");
        act.Should().Throw<InvalidSymbolException>();
    }

    [Fact]
    public void Quantity_rejects_negative()
    {
        var act = () => Quantity.Create(-0.5);
        act.Should().Throw<NegativeQuantityException>();
    }

    [Fact]
    public void Quantity_accepts_zero_and_positive()
    {
        Quantity.Create(0).Value.Should().Be(0);
        Quantity.Create(12.5).Value.Should().Be(12.5);
    }

    [Theory]
    [InlineData("2024-01-15", "2024-01-15")]
    [InlineData("2024/01/15", "2024-01-15")]
    public void TradeDate_normalises_iso(string input, string expected) =>
        TradeDate.Create(input).Value.Should().Be(expected);

    [Fact]
    public void TradeDate_rejects_garbage()
    {
        var act = () => TradeDate.Create("not-a-date");
        act.Should().Throw<InvalidDateException>();
    }

    [Theory]
    [InlineData("#6366f1")]
    [InlineData("#fff")]
    public void Color_accepts_hex(string input) => Color.Create(input).Value.Should().Be(input);

    [Theory]
    [InlineData("red")]
    [InlineData("6366f1")]
    [InlineData("#xyzxyz")]
    public void Color_rejects_non_hex(string input)
    {
        var act = () => Color.Create(input);
        act.Should().Throw<InvalidColorException>();
    }

    [Fact]
    public void Money_adds_within_same_currency()
    {
        var sum = new Money(10, CurrencyCode.Usd).Add(new Money(5, CurrencyCode.Usd));
        sum.Amount.Should().Be(15);
        sum.Currency.Should().Be(CurrencyCode.Usd);
    }

    [Fact]
    public void Money_rejects_mixed_currency_arithmetic()
    {
        var act = () => new Money(10, CurrencyCode.Usd).Add(new Money(5, CurrencyCode.Eur));
        act.Should().Throw<CurrencyMismatchException>();
    }

    [Fact]
    public void Money_equality_is_value_based() =>
        new Money(10, CurrencyCode.Usd).Should().Be(new Money(10, CurrencyCode.Usd));
}
