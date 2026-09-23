using Server.Domain.Transactions;
using Server.Infrastructure.Persistence;
using Server.Infrastructure.Services;
using Server.Infrastructure.Services.Import;

namespace Server.Tests;

public class ImportTimeTests
{
    [Fact]
    public void Trezor_unix_timestamp_is_the_authoritative_utc_instant()
    {
        // 1780000000 = 2026-05-28T20:26:40Z; the local columns say 10:26:40 PM GMT+2
        var t = ImportTimeParser.Trezor("1780000000", "5/28/2026", "10:26:40 PM GMT+2")!;
        t.Utc.Should().Be(new DateTime(2026, 5, 28, 20, 26, 40, DateTimeKind.Utc));
        t.Utc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        t.Date.Should().Be("2026-05-28");
    }

    [Fact]
    public void Just_after_local_midnight_belongs_to_the_previous_utc_day()
    {
        // 00:30 local (GMT+2) on Sep 8 is 22:30 UTC on Sep 7 — the date daily prices are looked up for
        var t = ImportTimeParser.Trezor(null, "9/8/2026", "12:30:00 AM GMT+2")!;
        t.Utc.Should().Be(new DateTime(2026, 9, 7, 22, 30, 0, DateTimeKind.Utc));
        t.Date.Should().Be("2026-09-07");
        t.LocalDate.Should().Be("2026-09-08");
    }

    [Theory]
    [InlineData("1/23/2026", "8:42:17 PM GMT+1", 2026, 1, 23, 19, 42, 17)]   // winter: CET = GMT+1
    [InlineData("7/23/2026", "8:42:17 PM GMT+2", 2026, 7, 23, 18, 42, 17)]   // summer: CEST = GMT+2
    [InlineData("3/29/2026", "3:30:00 AM GMT+2", 2026, 3, 29, 1, 30, 0)]     // first hour after the DST switch
    public void Local_fallback_honours_the_dst_dependent_offset(string date, string time, int y, int mo, int d, int h, int mi, int s)
    {
        ImportTimeParser.Trezor("", date, time)!.Utc.Should().Be(new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-03-01T14:30:00.123Z", "2026-03-01")]
    [InlineData("2026-03-01T01:30:00+02:00", "2026-02-28")]
    [InlineData("2026-03-01", "2026-03-01")]
    public void Iso_values_are_normalised_to_utc(string value, string utcDate)
    {
        ImportTimeParser.Iso(value)!.Date.Should().Be(utcDate);
    }

    [Fact]
    public async Task Imported_trezor_rows_keep_their_instant_and_full_transaction_id()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        const string txid = "aa11bb22cc33dd44ee55ff66aa77bb88cc99dd00ee11ff22aa33bb44cc55dd66";
        var csv = "Timestamp,Date,Time,Type,Transaction ID,Fee,Fee unit,Address,Label,Amount,Amount unit,Fiat (USD),Other\n" +
                  $"1780000000,5/28/2026,10:26:40 PM GMT+2,RECV,{txid},,,bc1qexampleaddress0000000000000000000,,0.5,BTC,\"30,000.00\",\n";

        using (var db = t.NewContext()) await new ImporterService(db).ImportAsync(csv, accountId, null);

        using (var db = t.NewContext())
        {
            var tx = db.Transactions.Single();
            tx.OccurredAt.Should().Be(new DateTime(2026, 5, 28, 20, 26, 40, DateTimeKind.Utc));
            tx.OccurredAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
            tx.Date.Value.Should().Be("2026-05-28");
            tx.ExternalId.Should().Be(txid);
        }
    }

    [Fact]
    public async Task Backup_round_trip_keeps_staking_instant_and_source_id()
    {
        using var t = new TestDb();
        var accountId = t.AddAccount();
        var when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        using (var db = t.NewContext())
        {
            db.Transactions.Add(Transaction.Create(accountId, Symbol.Create("SOL-USD"), TransactionType.TransferOut,
                Quantity.Create(1.5m), 100m, 0m, CurrencyCode.Usd, TradeDate.Create("2026-01-02"), null,
                isStaked: true, occurredAt: when, externalId: "sig-123"));
            db.SaveChanges();
        }

        var snapshot = await new DatabaseBackupService(t.NewContext()).ExportAsync(default);
        await new DatabaseBackupService(t.NewContext()).ImportAsync(snapshot, default);

        using (var db = t.NewContext())
        {
            var tx = db.Transactions.Single();
            tx.IsStaked.Should().BeTrue();
            tx.OccurredAt.Should().Be(when);
            tx.ExternalId.Should().Be("sig-123");
        }
    }
}
