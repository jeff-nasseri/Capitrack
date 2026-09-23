using Server.Domain.Holdings;
using Server.Domain.Transactions;

namespace Server.Tests;

/// <summary>Average-cost basis through buys, partial sells, fees and transfers.</summary>
public class CostBasisTests
{
    private static int _id;

    private static Transaction Tx(string symbol, TransactionType type, decimal qty, decimal price, decimal fee = 0,
        string date = "2026-01-01", int accountId = 1, string? externalId = null, bool staked = false, DateTime? at = null)
    {
        var tx = Transaction.Create(accountId, Symbol.Create(symbol), type, Quantity.Create(qty), price, fee,
            CurrencyCode.Usd, TradeDate.Create(date), null, isStaked: staked, occurredAt: at, externalId: externalId);
        typeof(Transaction).BaseType!.BaseType!.GetProperty("Id")!.SetValue(tx, ++_id); // stable tie-break order
        return tx;
    }

    [Fact]
    public void A_partial_sale_keeps_the_average_cost_of_the_remaining_units()
    {
        var h = HoldingsCalculator.ForAccount([
            Tx("ACME", TransactionType.Buy, 10, 100, date: "2026-01-01"),
            Tx("ACME", TransactionType.Buy, 10, 200, date: "2026-01-02"),
            Tx("ACME", TransactionType.Sell, 5, 300, date: "2026-01-03"),
        ]).Single();

        h.Quantity.Should().Be(15);
        h.AvgCost.Should().Be(150);
        h.TotalCost.Should().Be(2250);
    }

    [Fact]
    public void A_buy_commission_is_part_of_the_cost()
    {
        HoldingsCalculator.ForAccount([Tx("ACME", TransactionType.Buy, 10, 100, fee: 10)]).Single().AvgCost.Should().Be(101);
    }

    [Fact]
    public void A_network_fee_spends_units_at_the_average_cost()
    {
        var h = HoldingsCalculator.ForAccount([
            Tx("BTC-USD", TransactionType.TransferIn, 1, 30000),
            Tx("BTC-USD", TransactionType.Fee, 0.001m, 31000, date: "2026-01-02"),
        ]).Single();

        h.Quantity.Should().Be(0.999m);
        h.AvgCost.Should().Be(30000);
        h.TotalCost.Should().Be(29970);
    }

    [Fact]
    public void Moving_coins_between_own_accounts_carries_the_cost_basis_instead_of_buying()
    {
        var txs = new[]
        {
            Tx("BTC-USD", TransactionType.Buy, 1, 20000, accountId: 1, date: "2026-01-01"),
            Tx("BTC-USD", TransactionType.TransferOut, 0.5m, 50000, accountId: 1, date: "2026-02-01", externalId: "tx-move"),
            Tx("BTC-USD", TransactionType.Fee, 0.0001m, 50000, accountId: 1, date: "2026-02-01", externalId: "tx-move"),
            // the receiving wallet's row carries the market value at the time (50,000) — not a purchase
            Tx("BTC-USD", TransactionType.TransferIn, 0.5m, 50000, accountId: 2, date: "2026-02-01", externalId: "tx-move"),
        };

        var receiving = HoldingsCalculator.ForAccount(txs, 2).Single();
        receiving.TotalCost.Should().Be(10000);   // 0.5 × the sender's 20,000 average — not 25,000
        receiving.AvgCost.Should().Be(20000);

        var sending = HoldingsCalculator.ForAccount(txs, 1).Single();
        sending.Quantity.Should().Be(0.4999m);
        sending.TotalCost.Should().Be(9998);      // 0.4999 × 20,000

        var all = HoldingsCalculator.ByAccount(txs);
        all.Sum(h => h.Quantity).Should().Be(0.9999m);                 // the portfolio changed only by the fee
        all.Sum(h => h.Quantity * h.AvgCost).Should().Be(19998m);        // and its cost only by the fee's cost
    }

    [Fact]
    public void Both_legs_in_one_account_change_holdings_only_by_the_fee()
    {
        var h = HoldingsCalculator.ForAccount([
            Tx("ETH-USD", TransactionType.TransferIn, 2, 3000, date: "2026-01-01"),
            Tx("ETH-USD", TransactionType.TransferOut, 1, 3500, date: "2026-01-05", externalId: "tx-self"),
            Tx("ETH-USD", TransactionType.Fee, 0.002m, 3500, date: "2026-01-05", externalId: "tx-self"),
            Tx("ETH-USD", TransactionType.TransferIn, 1, 3500, date: "2026-01-05", externalId: "tx-self"),
        ]).Single();

        h.Quantity.Should().Be(1.998m);
        h.AvgCost.Should().Be(3000);
        h.TotalCost.Should().Be(5994);
    }

    [Fact]
    public void Coins_received_from_outside_capitrack_are_taken_at_their_value_when_received()
    {
        HoldingsCalculator.ForAccount([Tx("SOL-USD", TransactionType.TransferIn, 2, 150, externalId: "from-exchange")])
            .Single().TotalCost.Should().Be(300);
    }

    [Fact]
    public void A_staked_transfer_out_keeps_the_units_and_their_cost()
    {
        var h = HoldingsCalculator.ForAccount([
            Tx("SOL-USD", TransactionType.TransferIn, 3, 100),
            Tx("SOL-USD", TransactionType.TransferOut, 2, 120, date: "2026-01-02", staked: true),
        ]).Single();
        h.Quantity.Should().Be(3);
        h.TotalCost.Should().Be(300);
    }

    [Fact]
    public void Same_day_transactions_are_replayed_in_time_order()
    {
        var day2 = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var h = HoldingsCalculator.ForAccount([
            Tx("ACME", TransactionType.Buy, 1, 100, date: "2026-01-01"),
            Tx("ACME", TransactionType.Buy, 1, 200, date: "2026-01-02", at: day2.AddHours(12)),
            Tx("ACME", TransactionType.Sell, 1, 150, date: "2026-01-02", at: day2.AddHours(9)), // before the second buy
        ]).Single();

        h.AvgCost.Should().Be(200); // the 09:00 sale emptied the first lot; only the 12:00 buy remains
    }

    [Fact]
    public void Dust_balances_are_holdings_too()
    {
        HoldingsCalculator.ForAccount([Tx("SOL-USD", TransactionType.TransferIn, 0.000000001m, 0)])
            .Single().Quantity.Should().Be(0.000000001m);
    }

    [Fact]
    public void Cost_timeline_removes_cost_at_average_on_sales()
    {
        var timeline = HoldingsCalculator.CostTimeline([
            Tx("ACME", TransactionType.Buy, 10, 100, date: "2026-01-01"),
            Tx("ACME", TransactionType.Sell, 4, 250, date: "2026-01-02"),
        ]);
        timeline.Select(x => x.TotalCost).Should().Equal(1000m, 600m);
    }
}
