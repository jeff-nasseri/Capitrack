using Server.Application.Common.Interfaces;
using Server.Application.Transactions;
using Server.Domain.Holdings;
using Server.Infrastructure.Services;

namespace Server.Tests;

/// <summary>The Check &amp; Import preview must equal what the import then writes.</summary>
public class PreviewEqualsImportTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static List<ImportFileInput> Files(FileSelectionDto? utxoSelection = null) =>
    [
        new("trezor_btc_utxo.csv", Fixture("trezor_btc_utxo.csv"), utxoSelection),
        new("trezor_eth_account.csv", Fixture("trezor_eth_account.csv")),
    ];

    private static async Task AssertPreviewEqualsImport(TestDb t, int accountId, List<ImportFileInput> files)
    {
        ImportPreviewDto preview;
        using (var db = t.NewContext()) preview = await new ImporterService(db).PreviewAsync(accountId, files);

        ImportResultDto result;
        using (var db = t.NewContext()) result = await new ImporterService(db).ImportFilesAsync(accountId, files);

        // counts: rows read = new + duplicates + rejected + unselected, per file
        foreach (var f in preview.Files)
            (f.Summary.New + f.Summary.Duplicates + f.Summary.Rejected + f.Summary.Unselected).Should().Be(f.Summary.RowsRead);
        result.Total.Should().Be(preview.Files.Sum(f => f.Summary.RowsRead));
        result.Rejected.Should().Be(preview.Files.Sum(f => f.Summary.Rejected));
        result.Imported.Should().Be(preview.Files.SelectMany(f => f.Rows).SelectMany(r => r.Legs).Count(l => l.Status == "new"));

        // balances: what the preview promised is what the account now holds
        using var check = t.NewContext();
        var actual = HoldingsCalculator.ForAccount(check.Transactions.Where(x => x.AccountId == accountId).ToList())
            .ToDictionary(h => h.Symbol.Value, h => h.Quantity);
        foreach (var asset in preview.Files.SelectMany(f => f.Summary.Assets))
            actual.GetValueOrDefault(asset.Symbol).Should().Be(asset.ResultingBalance, asset.Symbol);
    }

    [Fact]
    public async Task Importing_everything_matches_the_preview()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        await AssertPreviewEqualsImport(t, accountId, Files());
    }

    [Fact]
    public async Task A_row_selection_with_a_staked_outflow_matches_the_preview()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        // rows 1, 4 and 5: the 0.5 receipt and the two-output send (row 4 carries the fee), with row 5's output staked
        var selection = new FileSelectionDto([1, 4, 5], [5]);
        await AssertPreviewEqualsImport(t, accountId, Files(selection));

        using var db = t.NewContext();
        var btc = db.Transactions.ToList().Where(x => x.Symbol.Value == "BTC-USD").ToList();
        btc.Should().HaveCount(4);                                   // receipt, two outputs, one fee
        btc.Single(x => x.Quantity.Value == 0.05m).IsStaked.Should().BeTrue();
    }

    [Fact]
    public async Task A_second_preview_after_importing_shows_only_duplicates_and_matches_again()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        using (var db = t.NewContext()) await new ImporterService(db).ImportFilesAsync(accountId, Files());

        ImportPreviewDto again;
        using (var db = t.NewContext()) again = await new ImporterService(db).PreviewAsync(accountId, Files());
        again.Files.Sum(f => f.Summary.New).Should().Be(0);
        again.Files.SelectMany(f => f.Summary.Assets).Should().OnlyContain(a => a.CurrentBalance == a.ResultingBalance);
        await AssertPreviewEqualsImport(t, accountId, Files());
    }

    [Fact]
    public async Task Rejected_rows_carry_their_reason_into_the_preview()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        ImportPreviewDto preview;
        using (var db = t.NewContext()) preview = await new ImporterService(db).PreviewAsync(accountId, Files());

        var eth = preview.Files[1];
        eth.Summary.Rejected.Should().Be(4);
        eth.Rows.Where(r => r.Status == "rejected").Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Reason));
        eth.Summary.CountsByType.Should().ContainKey("fee").WhoseValue.Should().Be(3);
        eth.Summary.Assets.Single(a => a.Symbol == "ETH-USD").Fees.Should().Be(0.0018m);
    }
}
