using Microsoft.EntityFrameworkCore;
using Server.Domain.Holdings;
using Server.Domain.Transactions;
using Server.Application.Transactions;
using Server.Infrastructure.Services;

namespace Server.Tests;

/// <summary>Re-importing the same or overlapping exports must change nothing (synthetic fixtures).</summary>
public class IdempotencyTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static async Task<ImportResultDto> Import(TestDb t, int accountId, params (string Name, string Content)[] files)
    {
        using var db = t.NewContext();
        var importer = new ImporterService(db);
        var plan = await importer.PlanAsync(accountId, files.Select(f => new ImportFileRequest(f.Name, f.Content)).ToList());
        return await importer.ApplyAsync(plan);
    }

    private static (string Name, string Content) F(string name) => (name, Fixture(name));

    /// <summary>Everything that matters about an account's ledger: count, and every row's identity and quantity.</summary>
    private static string Fingerprint(TestDb t, int accountId)
    {
        using var db = t.NewContext();
        var txs = db.Transactions.Where(x => x.AccountId == accountId).ToList();
        var balances = HoldingsCalculator.ForAccount(txs).OrderBy(h => h.Symbol.Value).Select(h => $"{h.Symbol.Value}={h.Quantity}");
        var rows = txs.OrderBy(x => x.ImportKey).Select(x => $"{x.ImportKey}:{x.Type.Value}:{x.Quantity.Value}:{x.OccurredAt:O}");
        return $"{txs.Count}|{string.Join(",", balances)}|{string.Join(",", rows)}";
    }

    [Theory]
    [InlineData("trezor_btc_utxo.csv")]
    [InlineData("trezor_eth_account.csv")]
    [InlineData("trezor_btc_identical.csv")]
    [InlineData("revolut_stocks.csv")]
    [InlineData("revolut_commodities.csv")]
    [InlineData("generic_identical.csv")]
    public async Task Importing_the_same_file_again_changes_nothing(string fixture)
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        var first = await Import(t, accountId, F(fixture));
        first.Imported.Should().BeGreaterThan(0);
        var after1 = Fingerprint(t, accountId);

        for (var i = 0; i < 2; i++)
        {
            var again = await Import(t, accountId, F(fixture));
            again.Imported.Should().Be(0);
            again.Updated.Should().Be(0);
            Fingerprint(t, accountId).Should().Be(after1);
        }
    }

    [Fact]
    public async Task An_overlapping_subset_and_the_same_file_twice_in_one_batch_change_nothing()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        await Import(t, accountId, F("trezor_btc_utxo.csv"));
        var baseline = Fingerprint(t, accountId);

        var lines = Fixture("trezor_btc_utxo.csv").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var subset = string.Join('\n', lines.Take(4)) + "\n"; // header + first three rows
        var result = await Import(t, accountId, ("subset.csv", subset), F("trezor_btc_utxo.csv"));

        result.Imported.Should().Be(0);
        Fingerprint(t, accountId).Should().Be(baseline);
    }

    [Fact]
    public async Task Keys_ignore_fiat_values_labels_and_row_order()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        await Import(t, accountId, F("trezor_btc_utxo.csv"));
        var baseline = Fingerprint(t, accountId);

        // a later export: fiat revalued, a label added, rows in a different order
        var lines = Fixture("trezor_btc_utxo.csv").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var reordered = new[] { lines[0] }.Concat(lines.Skip(1).Reverse())
            .Select(l => l.Replace("\"45,000.00\"", "\"47,123.45\"").Replace(",bc1qsyntheticreceive000000000000000000001,,", ",bc1qsyntheticreceive000000000000000000001,Savings,"));
        var result = await Import(t, accountId, ("later-export.csv", string.Join('\n', reordered) + "\n"));

        result.Imported.Should().Be(0);
        Fingerprint(t, accountId).Should().Be(baseline);
    }

    [Fact]
    public async Task Genuinely_identical_rows_are_all_imported_and_counted_on_reimport()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();

        // two separate same-day 0.001 BTC receipts + one transaction paying the same address twice
        (await Import(t, accountId, F("trezor_btc_identical.csv"))).Imported.Should().Be(4);
        (await Import(t, accountId, F("generic_identical.csv"))).Imported.Should().Be(3);
        (await Import(t, accountId, F("revolut_stocks.csv"))).Imported.Should().Be(4); // 2 identical buys + sell + dividend

        using var db = t.NewContext();
        var holdings = HoldingsCalculator.ForAccount(db.Transactions.ToList()).ToDictionary(h => h.Symbol.Value, h => h.Quantity);
        holdings["BTC-USD"].Should().Be(0.042m);
        holdings["ACME"].Should().Be(1m + 3m); // generic: 1 + 1 - 1; Revolut: 2 + 2 - 1
    }

    [Fact]
    public async Task Both_legs_of_an_own_wallet_transfer_import_into_one_account_and_only_the_fee_changes_holdings()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        var result = await Import(t, accountId, F("trezor_wallet_a.csv"), F("trezor_wallet_b.csv"));

        result.Imported.Should().Be(4); // receipt, send, its fee, the receiving leg — same tx id, different keys
        using var db = t.NewContext();
        HoldingsCalculator.ForAccount(db.Transactions.ToList()).Single().Quantity.Should().Be(0.9999m);
    }

    [Fact]
    public async Task A_pending_transaction_confirmed_later_is_updated_not_duplicated()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        await Import(t, accountId, F("trezor_pending_v1.csv"));

        var result = await Import(t, accountId, F("trezor_pending_v2.csv"));
        result.Imported.Should().Be(0);
        result.Updated.Should().Be(2); // the send and its fee now carry the confirmation time

        using var db = t.NewContext();
        var txs = db.Transactions.ToList();
        txs.Should().HaveCount(3); // receipt, send, fee — the send and fee refreshed, not re-added
        var send = txs.Single(x => x.Type == TransactionType.TransferOut);
        send.OccurredAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1767607200).UtcDateTime);
        HoldingsCalculator.ForAccount(txs).Single().Quantity.Should().Be(1.498m);
    }

    [Fact]
    public void The_database_rejects_a_second_row_with_the_same_import_key()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using var db = t.NewContext();
        Transaction Row() => Transaction.Create(accountId, Symbol.Create("BTC-USD"), TransactionType.TransferIn,
            Quantity.Create(1m), 0m, 0m, CurrencyCode.Usd, TradeDate.Create("2026-01-01"), null, importKey: "trezor|x|in|BTC|a|1#0");
        db.Transactions.Add(Row());
        db.SaveChanges();
        db.Transactions.Add(Row());
        var act = () => db.SaveChanges();
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public async Task An_import_that_fails_part_way_writes_nothing()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using var db = t.NewContext();
        var importer = new ImporterService(db);
        var plan = await importer.PlanAsync(accountId, [new ImportFileRequest("a.csv", Fixture("trezor_btc_utxo.csv"))]);

        // a concurrent import writes one of the planned rows first
        var raced = plan.Legs.Last().Leg;
        using (var other = t.NewContext())
        {
            other.Transactions.Add(Transaction.Create(accountId, Symbol.Create(raced.Symbol), TransactionType.From(raced.Type),
                Quantity.Create(raced.Quantity), 0m, 0m, CurrencyCode.Usd, TradeDate.Create(raced.Date), null, importKey: raced.Key));
            other.SaveChanges();
        }

        var result = await importer.ApplyAsync(plan);
        result.Imported.Should().Be(0);
        result.Errors.Should().ContainSingle().Which.Should().StartWith("Import aborted");
        using var check = t.NewContext();
        check.Transactions.Count().Should().Be(1); // only the concurrent row
    }

    [Fact]
    public async Task Rows_imported_before_import_keys_existed_are_adopted_not_duplicated()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using (var db = t.NewContext())
        {
            // what the old importer stored: local date, coin fee in the money field, and only ONE of two
            // identical same-day receipts (its fingerprint merged them)
            db.Transactions.Add(Transaction.Create(accountId, Symbol.Create("BTC-USD"), TransactionType.TransferIn,
                Quantity.Create(0.001m), 90000m, 0m, CurrencyCode.Usd, TradeDate.Create("2026-01-02"), "TxID: a0000000..."));
            db.Transactions.Add(Transaction.Create(accountId, Symbol.Create("BTC-USD"), TransactionType.TransferOut,
                Quantity.Create(0.3m), 90000m, 0.0001m, CurrencyCode.Usd, TradeDate.Create("2026-01-04"), "TxID: b0000000...", isStaked: true));
            db.SaveChanges();
        }

        var result = await Import(t, accountId, F("trezor_btc_identical.csv"), F("trezor_wallet_a.csv"));
        result.Imported.Should().Be(5);     // 3 identical-file legs not yet present + wallet-a receipt + the send's fee leg
        result.Skipped.Should().Be(2);      // the two legacy rows, adopted

        using var check = t.NewContext();
        var txs = check.Transactions.ToList();
        txs.Should().HaveCount(7).And.OnlyContain(x => x.ImportKey != null);
        var send = txs.Single(x => x.Type == TransactionType.TransferOut);
        send.IsStaked.Should().BeTrue();   // the user's choice survives adoption
        send.Fee.Should().Be(0m);           // the coin fee is now its own `fee` leg
        send.ExternalId.Should().StartWith("b000");
    }
}
