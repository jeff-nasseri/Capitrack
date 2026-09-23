using Server.Domain.Holdings;
using Server.Infrastructure.Services;
using Server.Infrastructure.Services.Import;

namespace Server.Tests;

/// <summary>
/// Trezor Suite classification on synthetic exports (made-up ids, addresses and amounts). Expected
/// numbers come from tools/reference/trezor_reference.py, which is independent of the importer.
/// </summary>
public class TrezorLedgerTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Utxo_multi_output_send_counts_every_output_and_the_fee_once()
    {
        var file = CsvImportParser.Parse(Fixture("trezor_btc_utxo.csv"));
        file.Format.Should().Be("trezor");
        file.Rows.Should().HaveCount(6).And.OnlyContain(r => !r.IsRejected);

        var legs = file.Rows.SelectMany(r => r.Legs).ToList();
        legs.Where(l => l.Type == "transfer_out").Select(l => l.Quantity).Should().BeEquivalentTo([0.1m, 0.05m]);
        legs.Where(l => l.Type == "fee").Select(l => l.Quantity).Should().BeEquivalentTo([0.0002m, 0.0001m]); // SENT once + SELF
    }

    [Fact]
    public void Self_transfer_charges_only_the_fee()
    {
        var self = CsvImportParser.Parse(Fixture("trezor_btc_utxo.csv")).Rows[5];
        self.Legs.Should().ContainSingle().Which.Should().Match<ImportLeg>(l => l.Type == "fee" && l.Quantity == 0.0001m);
    }

    [Fact]
    public void Account_chain_rows_are_classified_with_reasons_and_nothing_is_dropped_silently()
    {
        var rows = CsvImportParser.Parse(Fixture("trezor_eth_account.csv")).Rows;
        rows.Should().HaveCount(9);

        rows[2].Rejection.Should().StartWith("Internal contract transfer");      // 2nd native row of one SENT
        rows[6].Rejection.Should().Contain("NFT");                               // "ID 7"
        rows[7].Rejection.Should().StartWith("Nothing to record");               // 0 ETH, no fee
        rows[8].Rejection.Should().Contain("Unsupported transaction type");     // SWAP

        // token transfer: USDC leaves, the fee leaves in ETH (not USDC)
        rows[4].Legs.Should().BeEquivalentTo(new[]
        {
            new { Symbol = "USDC-USD", Type = "transfer_out", Quantity = 25m },
            new { Symbol = "ETH-USD", Type = "fee", Quantity = 0.0005m }
        }, o => o.ExcludingMissingMembers());

        // a failed transaction still burns its fee
        rows[5].Legs.Should().ContainSingle().Which.Should().Match<ImportLeg>(l => l.Symbol == "ETH-USD" && l.Type == "fee" && l.Quantity == 0.0003m);
    }

    [Fact]
    public async Task Imported_balances_and_fees_match_the_reference_exactly()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using (var db = t.NewContext())
        {
            var importer = new ImporterService(db);
            (await importer.ImportAsync(Fixture("trezor_btc_utxo.csv"), accountId, null)).Rejected.Should().Be(0);
            var eth = await importer.ImportAsync(Fixture("trezor_eth_account.csv"), accountId, null);
            eth.Total.Should().Be(9);
            eth.Rejected.Should().Be(4);
            eth.Rejections.Should().HaveCount(4);
        }

        using (var db = t.NewContext())
        {
            var txs = db.Transactions.ToList();
            var balances = HoldingsCalculator.ForAccount(txs).ToDictionary(h => h.Symbol.Value, h => h.Quantity);
            balances["BTC-USD"].Should().Be(0.3527m);
            balances["ETH-USD"].Should().Be(0.5982m);
            balances["USDC-USD"].Should().Be(75m);

            var fees = txs.Where(x => x.Type.Value == "fee").GroupBy(x => x.Symbol.Value).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity.Value));
            fees["BTC-USD"].Should().Be(0.0003m);
            fees["ETH-USD"].Should().Be(0.0018m);
            fees.Should().NotContainKey("USDC-USD");
        }
    }

    [Fact]
    public async Task European_semicolon_export_with_decimal_commas_matches_the_reference()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using (var db = t.NewContext()) await new ImporterService(db).ImportAsync(Fixture("trezor_btc_eu_semicolon.csv"), accountId, null);

        using (var db = t.NewContext())
        {
            HoldingsCalculator.ForAccount(db.Transactions.ToList()).Single().Quantity.Should().Be(0.2499m);
            db.Transactions.ToList().Single(x => x.Type.Value == "transfer_in").Price.Should().Be(45001m); // 22.500,50 / 0,5
        }
    }
}
