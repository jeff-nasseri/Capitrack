using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Server.Infrastructure.Persistence;

/// <summary>
/// Brings an EXISTING SQLite database up to the current EF model. <c>EnsureCreated</c> only
/// builds schema on a brand-new database — it never alters one that already has tables — so
/// without this step any entity or column added in a release would break deployments that
/// keep their data volume. The upgrade is model-driven (nothing hard-coded): it diffs the
/// live schema against the EF model, creates missing tables (with their indexes) from EF's
/// own create script, and ADDs missing columns with their mapped type/nullability/default.
/// Destructive changes (drops, renames, type changes) are intentionally out of scope.
/// </summary>
public static class SqliteSchemaUpgrader
{
    /// <summary>Applies any missing tables/columns. Idempotent; a fresh database is a no-op.</summary>
    public static void Upgrade(CapitrackDbContext db, ILogger? logger = null)
    {
        var existingTables = QuerySingleColumn(db, "SELECT name FROM sqlite_master WHERE type = 'table'")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existingTables.Count == 0) return; // fresh database — EnsureCreated built everything

        CreateMissingTables(db, existingTables, logger);
        AddMissingColumns(db, existingTables, logger);
    }

    /// <summary>Executes the statements of EF's create script that target tables not yet in the database.</summary>
    private static void CreateMissingTables(CapitrackDbContext db, HashSet<string> existingTables, ILogger? logger)
    {
        // Each DDL statement ends with ';' at end-of-line; the bodies contain no semicolons.
        var statements = Regex.Split(db.Database.GenerateCreateScript(), @";\s*(?:\r?\n|$)")
            .Select(s => s.Trim()).Where(s => s.Length > 0);

        foreach (var sql in statements)
        {
            var table =
                Regex.Match(sql, "^CREATE TABLE \"([^\"]+)\"", RegexOptions.IgnoreCase) is { Success: true } t ? t.Groups[1].Value :
                Regex.Match(sql, " ON \"([^\"]+)\"", RegexOptions.IgnoreCase) is { Success: true } i ? i.Groups[1].Value : null;
            if (table is null || existingTables.Contains(table)) continue;

            logger?.LogInformation("Schema upgrade: creating missing object for table {Table}", table);
            db.Database.ExecuteSqlRaw(sql);
        }
    }

    /// <summary>ALTERs existing tables to add any column present in the model but absent in the database.</summary>
    private static void AddMissingColumns(CapitrackDbContext db, HashSet<string> existingTables, ILogger? logger)
    {
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null || !existingTables.Contains(table)) continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            var existingColumns = QuerySingleColumn(db, $"SELECT name FROM pragma_table_info('{table}')")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var property in entity.GetProperties())
            {
                var column = property.GetColumnName(store);
                if (column is null || existingColumns.Contains(column)) continue;

                var ddl = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {property.GetColumnType()}";
                if (!property.IsColumnNullable(store))
                    ddl += $" NOT NULL DEFAULT {DefaultLiteral(property, store)}";

                logger?.LogInformation("Schema upgrade: {Ddl}", ddl);
                db.Database.ExecuteSqlRaw(ddl);
            }
        }
    }

    /// <summary>
    /// A constant DEFAULT for the ADD COLUMN (SQLite forbids non-constant defaults there, so
    /// expressions like CURRENT_TIMESTAMP fall back to a type-appropriate zero value).
    /// </summary>
    private static string DefaultLiteral(IProperty property, StoreObjectIdentifier store)
    {
        var sql = property.GetDefaultValueSql(store);
        if (sql is not null && !sql.Contains('(') && !sql.Contains("CURRENT", StringComparison.OrdinalIgnoreCase))
            return sql;

        if (property.TryGetDefaultValue(store, out var value) && value is not null)
            return value switch
            {
                bool b => b ? "1" : "0",
                string s => $"'{s.Replace("'", "''")}'",
                DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"
            };

        return property.GetColumnType().Contains("TEXT", StringComparison.OrdinalIgnoreCase) ? "''" : "0";
    }

    private static List<string> QuerySingleColumn(CapitrackDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<string>();
        while (reader.Read()) values.Add(reader.GetString(0));
        return values;
    }
}
