using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Persistence;

namespace Server.Tests;

/// <summary>
/// Upgrades a COPY of a real, pre-decimal Capitrack database and checks nothing is lost. Runs only
/// when <c>CAPITRACK_LEGACY_DB</c> points at such a file (no private data lives in the repo);
/// otherwise it passes without doing anything.
/// </summary>
public class LegacyDatabaseMigrationTests
{
    [Fact]
    public void Upgrading_a_legacy_database_preserves_every_transaction_value()
    {
        var source = Environment.GetEnvironmentVariable("CAPITRACK_LEGACY_DB");
        if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;

        var copy = Path.Combine(Path.GetTempPath(), $"capitrack-legacy-{Guid.NewGuid():N}.db");
        File.Copy(source, copy);
        try
        {
            var before = new List<(long Id, double Quantity, double Price, double Fee)>();
            using (var raw = new SqliteConnection($"Data Source={copy}"))
            {
                raw.Open();
                using var cmd = raw.CreateCommand();
                cmd.CommandText = "SELECT Id, Quantity, Price, Fee FROM Transactions ORDER BY Id";
                using var r = cmd.ExecuteReader();
                while (r.Read()) before.Add((r.GetInt64(0), r.GetDouble(1), r.GetDouble(2), r.GetDouble(3)));
            }
            before.Should().NotBeEmpty();

            var options = new DbContextOptionsBuilder<CapitrackDbContext>().UseSqlite($"Data Source={copy}").Options;
            using (var db = new CapitrackDbContext(options)) SqliteSchemaUpgrader.Upgrade(db);

            using (var db = new CapitrackDbContext(options))
            {
                var after = db.Transactions.OrderBy(t => t.Id).ToList();
                after.Select(t => (long)t.Id).Should().Equal(before.Select(b => b.Id));
                for (var i = 0; i < before.Count; i++)
                {
                    after[i].Quantity.Value.Should().Be((decimal)before[i].Quantity);
                    after[i].Price.Should().Be((decimal)before[i].Price);
                    after[i].Fee.Should().Be((decimal)before[i].Fee);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(copy);
        }
    }
}
