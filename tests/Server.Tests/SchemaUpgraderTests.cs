using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Infrastructure.Persistence;

namespace Server.Tests;

public class SchemaUpgraderTests
{
    public sealed class LedgerRow
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; } = "";
    }

    public sealed class LedgerDb(DbContextOptions<LedgerDb> options) : DbContext(options)
    {
        public DbSet<LedgerRow> Rows => Set<LedgerRow>();
    }

    [Fact]
    public void Legacy_real_column_for_a_decimal_property_is_rebuilt_as_text_and_keeps_its_values()
    {
        var path = Path.Combine(Path.GetTempPath(), $"capitrack-upgrade-{Guid.NewGuid():N}.db");
        try
        {
            using (var legacy = new SqliteConnection($"Data Source={path}"))
            {
                legacy.Open();
                Exec(legacy, "CREATE TABLE \"Rows\" (\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_Rows\" PRIMARY KEY AUTOINCREMENT, \"Amount\" REAL NOT NULL, \"Note\" TEXT NOT NULL)");
                Exec(legacy, "INSERT INTO \"Rows\" (\"Amount\", \"Note\") VALUES (0.1, 'a'), (0.00000001, 'b'), (12345.678901234, 'c')");
            }
            var options = new DbContextOptionsBuilder<LedgerDb>().UseSqlite($"Data Source={path}").Options;

            using (var db = new LedgerDb(options)) SqliteSchemaUpgrader.Upgrade(db);

            using (var db = new LedgerDb(options))
            {
                ColumnType(db, "Rows", "Amount").Should().Be("TEXT");
                db.Rows.OrderBy(r => r.Id).Select(r => r.Amount).ToList()
                    .Should().Equal(0.1m, 0.00000001m, 12345.678901234m);

                // an 18-decimal amount would lose precision in a REAL column; in TEXT it round-trips exactly
                db.Rows.Add(new LedgerRow { Amount = 0.123456789012345678m, Note = "d" });
                db.SaveChanges();
            }
            using (var db = new LedgerDb(options))
            {
                db.Rows.Single(r => r.Note == "d").Amount.Should().Be(0.123456789012345678m);
                SqliteSchemaUpgrader.Upgrade(db); // idempotent: nothing left to change
                db.Rows.Count().Should().Be(4);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static void Exec(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ColumnType(DbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT type FROM pragma_table_info('{table}') WHERE name = '{column}'";
        return (string)command.ExecuteScalar()!;
    }
}
