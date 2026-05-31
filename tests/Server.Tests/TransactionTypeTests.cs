namespace Server.Tests;

public class TransactionTypeTests
{
    [Theory]
    [InlineData("buy")]
    [InlineData("sell")]
    [InlineData("transfer_in")]
    [InlineData("transfer_out")]
    [InlineData("dividend")]
    [InlineData("interest")]
    [InlineData("fee")]
    public void From_accepts_all_valid_types(string value) =>
        TransactionType.From(value).Value.Should().Be(value);

    [Fact]
    public void From_is_case_insensitive() =>
        TransactionType.From("BUY").Should().Be(TransactionType.Buy);

    [Fact]
    public void From_rejects_unknown_type()
    {
        var act = () => TransactionType.From("gift");
        act.Should().Throw<InvalidTransactionTypeException>();
    }

    [Theory]
    [InlineData("sell", true)]
    [InlineData("gift", false)]
    [InlineData(null, false)]
    public void IsValid_reports_membership(string? value, bool expected) =>
        TransactionType.IsValid(value).Should().Be(expected);

    [Fact]
    public void Quantity_direction_flags_are_correct()
    {
        TransactionType.Buy.IncreasesQuantity.Should().BeTrue();
        TransactionType.TransferIn.IncreasesQuantity.Should().BeTrue();
        TransactionType.Sell.DecreasesQuantity.Should().BeTrue();
        TransactionType.TransferOut.DecreasesQuantity.Should().BeTrue();
        TransactionType.Dividend.IncreasesQuantity.Should().BeFalse();
    }

    [Fact]
    public void History_flags_include_dividend_in_add()
    {
        TransactionType.Buy.CountsAsHistoryAdd.Should().BeTrue();
        TransactionType.TransferIn.CountsAsHistoryAdd.Should().BeTrue();
        TransactionType.Dividend.CountsAsHistoryAdd.Should().BeTrue();
        TransactionType.Sell.CountsAsHistorySub.Should().BeTrue();
        TransactionType.TransferOut.CountsAsHistorySub.Should().BeTrue();
        TransactionType.Fee.CountsAsHistoryAdd.Should().BeFalse();
    }

    [Fact]
    public void All_contains_seven_types() => TransactionType.All.Should().HaveCount(7);
}
