using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Domain.Accounts;
using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;

namespace Server.Tests;

public class PrecisionTests
{
    [Fact]
    public void Eighteen_decimal_amounts_round_trip_exactly_through_the_database()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<CapitrackDbContext>().UseSqlite(connection).Options;

        // 28 significant digits: beyond double (≈15-17) and beyond JS safe integers in smallest units
        const decimal quantity = 1234567890.123456789012345678m;
        const decimal fee = 0.000000000000000001m;
        const decimal price = 3141.592653589m;

        using (var db = new CapitrackDbContext(options))
        {
            db.Database.EnsureCreated();
            var account = Account.Create("Wallet", AccountType.Crypto, CurrencyCode.Usd, "", "", Color.Default);
            db.Accounts.Add(account);
            db.SaveChanges();
            db.Transactions.Add(Transaction.Create(account.Id, Symbol.Create("ETH-USD"), TransactionType.TransferIn,
                Quantity.Create(quantity), price, fee, CurrencyCode.Usd, TradeDate.Create("2026-01-02"), null));
            db.SaveChanges();
        }

        using (var db = new CapitrackDbContext(options))
        {
            var tx = db.Transactions.Single();
            tx.Quantity.Value.Should().Be(quantity);
            tx.Fee.Should().Be(fee);
            tx.Price.Should().Be(price);
        }
    }

    [Fact]
    public void Decimal_sums_have_no_binary_float_artifacts()
    {
        // 0.1 + 0.2 is 0.30000000000000004 in binary floating point
        var sum = new[] { 0.1m, 0.2m }.Sum();
        sum.Should().Be(0.3m);
        (sum.ToString(System.Globalization.CultureInfo.InvariantCulture)).Should().Be("0.3");
    }
}
