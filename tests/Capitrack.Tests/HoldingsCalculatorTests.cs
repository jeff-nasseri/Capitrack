using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Xunit;

namespace Capitrack.Tests;

// Ports tests/wealth-calculation.test.js — Holdings Calculation block.
public class HoldingsCalculatorTests
{
    private static Transaction Tx(string symbol, string type, double qty, double price, double fee = 0, string date = "2024-01-15")
        => new() { Symbol = symbol, Type = type, Quantity = qty, Price = price, Fee = fee, Date = date, Currency = "USD" };

    [Fact]
    public void Buy_transactions_increase_quantity()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 185.50),
            Tx("AAPL", "buy", 5, 190.00, date: "2024-02-01")
        ]);
        Assert.Single(holdings);
        Assert.Equal(15, holdings[0].Quantity);
    }

    [Fact]
    public void Sell_transactions_reduce_quantity()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 185.50),
            Tx("AAPL", "sell", 3, 195.00, date: "2024-02-01")
        ]);
        Assert.Equal(7, holdings[0].Quantity);
    }

    [Fact]
    public void Fully_sold_holdings_are_filtered_out()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 185.50),
            Tx("AAPL", "sell", 10, 195.00, date: "2024-02-01")
        ]);
        Assert.DoesNotContain(holdings, h => h.Symbol == "AAPL");
    }

    [Fact]
    public void Transfer_in_and_out_affect_quantities()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("BTC-USD", "transfer_in", 0.5, 45000),
            Tx("BTC-USD", "transfer_out", 0.1, 46000, date: "2024-02-01")
        ]);
        Assert.Equal(0.4, holdings[0].Quantity, 8);
    }

    [Fact]
    public void Average_cost_is_weighted_by_quantity()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 100),
            Tx("AAPL", "buy", 10, 200, date: "2024-02-01")
        ]);
        Assert.Equal(150, holdings[0].AvgCost!.Value, 2);
    }

    [Fact]
    public void Total_cost_uses_buy_plus_fee_minus_sell()
    {
        // (10*185.5+1) + (5*190+1) - (3*210 - 1) = 1856 + 951 - 629 = 2178
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 185.50, 1),
            Tx("AAPL", "buy", 5, 190.00, 1, "2024-03-10"),
            Tx("AAPL", "sell", 3, 210.00, 1, "2024-06-01")
        ]);
        Assert.Equal(2178, holdings[0].TotalCost, 4);
    }

    [Fact]
    public void Multiple_symbols_are_separate_and_ordered_by_total_cost()
    {
        var holdings = HoldingsCalculator.AccountHoldings([
            Tx("AAPL", "buy", 10, 185),
            Tx("MSFT", "buy", 8, 370)
        ]);
        Assert.Equal(2, holdings.Count);
        Assert.Equal("MSFT", holdings[0].Symbol); // 2960 > 1850
        Assert.Equal("AAPL", holdings[1].Symbol);
    }
}
