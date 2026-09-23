using Server.Infrastructure.Services;

namespace Server.Tests;

public class ImportParsingTests
{
    [Fact]
    public async Task European_generic_csv_with_bom_semicolons_and_decimal_commas_parses_exactly()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount(type: "stock", currency: "EUR");
        const string csv = "﻿symbol;type;quantity;price;fee;currency;date\r\n" +
                           "ACME;buy;0,5;1.234,56;1,25;EUR;2026-01-02\r\n" +
                           "ACME;buy;2;10,1;0;EUR;2026-01-03\r\n";

        using (var db = t.NewContext())
        {
            var result = await new ImporterService(db).ImportAsync(csv, accountId, null);
            result.Format.Should().Be("generic");
            result.Imported.Should().Be(2);
        }

        using (var db = t.NewContext())
        {
            var rows = db.Transactions.OrderBy(x => x.Id).ToList();
            rows[0].Quantity.Value.Should().Be(0.5m);
            rows[0].Price.Should().Be(1234.56m);
            rows[0].Fee.Should().Be(1.25m);
            rows[1].Price.Should().Be(10.1m);
        }
    }

    [Fact]
    public async Task Quoted_thousands_separated_fiat_values_parse_exactly()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount(type: "stock", currency: "USD");
        const string csv = "symbol,type,quantity,price,fee,currency,date\n" +
                           "ACME,buy,3,\"1,284.75\",\"1,000.5\",USD,2026-01-02\n";

        using (var db = t.NewContext()) await new ImporterService(db).ImportAsync(csv, accountId, null);

        using (var db = t.NewContext())
        {
            var row = db.Transactions.Single();
            row.Price.Should().Be(1284.75m);
            row.Fee.Should().Be(1000.5m);
        }
    }
}
