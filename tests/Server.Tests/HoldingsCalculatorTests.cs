using Server.Domain.Holdings;
using Server.Domain.Transactions;

namespace Server.Tests;

public class HoldingsCalculatorTests
{
    private static Transaction Tx(string symbol, TransactionType type, double qty, double price,
        double fee = 0, string date = "2024-01-01", int accountId = 1) =>
        Transaction.Create(accountId, Symbol.Create(symbol), type, Quantity.Create(qty), price, fee,
            CurrencyCode.Usd, TradeDate.Create(date), null);

    [Fact]
    public void Buy_then_sell_reduces_quantity_and_cost()
    {
        var txs = new[]
        {
            Tx("AAPL", TransactionType.Buy, 10, 150, date: "2024-01-01"),
            Tx("AAPL", TransactionType.Sell, 4, 200, date: "2024-03-01"),
        };

        var holding = HoldingsCalculator.ForAccount(txs).Single();

        holding.Quantity.Should().Be(6);                 // 10 - 4
        holding.AvgCost.Should().Be(150);                // buyValue 1500 / buyQty 10
        holding.TotalCost.Should().Be(700);             // (10*150) - (4*200)
        holding.TransactionCount.Should().Be(2);
        holding.FirstTransaction.Should().Be("2024-01-01");
        holding.LastTransaction.Should().Be("2024-03-01");
    }

    [Fact]
    public void Transfer_in_adds_to_quantity()
    {
        var txs = new[]
        {
            Tx("BTC-USD", TransactionType.Buy, 1, 30000),
            Tx("BTC-USD", TransactionType.TransferIn, 0.5, 0),
        };

        var holding = HoldingsCalculator.ForAccount(txs).Single();
        holding.Quantity.Should().Be(1.5);
    }

    [Fact]
    public void Fully_sold_position_is_excluded()
    {
        var txs = new[]
        {
            Tx("AAPL", TransactionType.Buy, 10, 150),
            Tx("AAPL", TransactionType.Sell, 10, 200),
        };

        HoldingsCalculator.ForAccount(txs).Should().BeEmpty();
    }

    [Fact]
    public void Holdings_are_ordered_by_total_cost_descending()
    {
        var txs = new[]
        {
            Tx("AAPL", TransactionType.Buy, 1, 100),     // cost 100
            Tx("MSFT", TransactionType.Buy, 1, 300),     // cost 300
            Tx("TSLA", TransactionType.Buy, 1, 200),     // cost 200
        };

        var symbols = HoldingsCalculator.ForAccount(txs).Select(h => h.Symbol.Value).ToList();
        symbols.Should().ContainInOrder("MSFT", "TSLA", "AAPL");
    }

    [Fact]
    public void Fee_is_added_to_total_cost_on_buy()
    {
        var txs = new[] { Tx("AAPL", TransactionType.Buy, 10, 150, fee: 5) };
        HoldingsCalculator.ForAccount(txs).Single().TotalCost.Should().Be(1505);
    }

    [Fact]
    public void ByAccount_groups_by_symbol_and_account()
    {
        var txs = new[]
        {
            Tx("AAPL", TransactionType.Buy, 10, 150, accountId: 1),
            Tx("AAPL", TransactionType.Buy, 5, 100, accountId: 2),
        };

        var result = HoldingsCalculator.ByAccount(txs);
        result.Should().HaveCount(2);
        result.Should().Contain(h => h.AccountId == 1 && h.Quantity == 10 && h.AvgCost == 150);
        result.Should().Contain(h => h.AccountId == 2 && h.Quantity == 5 && h.AvgCost == 100);
    }
}
