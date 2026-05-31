using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Capitrack.Tests;

// Ports tests/importer.test.js — format detection, import, dedup, dividend parsing.
public class ImporterServiceTests
{
    [Theory]
    [InlineData("symbol,type,quantity,price\nAAPL,buy,1,100", "generic")]
    [InlineData("Date,Ticker,Type,Quantity,Price per share\n2024-01-01,AAPL,BUY - MARKET,1,100", "revolut-stocks")]
    [InlineData("Product,Started Date,State,Description,Amount\nX,2024-01-01,COMPLETED,Exchanged to EUR,1", "revolut-commodities")]
    [InlineData("Transaction ID,Type,Amount,Amount unit,Date\nabc,RECV,1,BTC,1/1/2024", "trezor")]
    [InlineData("foo,bar\n1,2", "unknown")]
    public void DetectFormat_identifies_known_layouts(string csv, string expected)
    {
        var (_, headers) = ImporterService.ParseCsv(csv);
        Assert.Equal(expected, ImporterService.DetectFormat(headers));
    }

    [Fact]
    public void Generic_import_then_reimport_deduplicates()
    {
        using var t = new TestDb();
        t.Db.Accounts.Add(new Account { Name = "Stock", Type = "stock", Currency = "USD" });
        t.Db.SaveChanges();
        var importer = new ImporterService(t.Db);

        const string csv = "symbol,type,quantity,price,fee,currency,date,notes\n" +
                           "NVDA,buy,10,450,1.5,USD,2024-07-01,\n" +
                           "TSLA,buy,3,250,1,USD,2024-07-15,\n";

        var first = importer.ImportCsv(csv, 1, null);
        Assert.Equal("generic", first.Format);
        Assert.Equal(2, first.Imported);
        Assert.Equal(0, first.Skipped);

        var second = importer.ImportCsv(csv, 1, null);
        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.Skipped);
        Assert.Equal(2, t.Db.Transactions.Count());
    }

    [Fact]
    public void Revolut_dividend_parsed_as_amount_with_unit_price()
    {
        using var t = new TestDb();
        t.Db.Accounts.Add(new Account { Name = "Stock", Type = "stock", Currency = "USD" });
        t.Db.SaveChanges();
        var importer = new ImporterService(t.Db);

        const string csv = "Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency\n" +
                           "2024-09-10T10:00:00.000Z,GOOGL,DIVIDEND,0,0,$12.50,USD\n";

        var result = importer.ImportCsv(csv, 1, null);
        Assert.Equal("revolut-stocks", result.Format);
        Assert.Equal(1, result.Imported);

        var div = t.Db.Transactions.Single(x => x.Type == "dividend");
        Assert.Equal("GOOGL", div.Symbol);
        Assert.Equal(12.5, div.Quantity, 4);
        Assert.Equal(1, div.Price, 4);
    }
}
