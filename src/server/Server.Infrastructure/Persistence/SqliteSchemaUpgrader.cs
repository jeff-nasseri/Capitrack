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
/// own create script, ADDs missing columns with their mapped type/nullability/default, and
/// rebuilds a table whose column storage type changed (e.g. REAL → TEXT when a property moves
/// from double to decimal), converting the stored values. Drops and renames are out of scope.
/// </summary>
public static class SqliteSchemaUpgrader
{
    private const string RebuildPrefix = "__rebuild_";

    /// <summary>Applies any missing tables/columns and storage-type changes. Idempotent; a fresh database is a no-op.</summary>
    public static void Upgrade(DbContext db, ILogger? logger = null)
    {
        var existingTables = QuerySingleColumn(db, "SELECT name FROM sqlite_master WHERE type = 'table'")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existingTables.Count == 0) return; // fresh database — EnsureCreated built everything

        var script = CreateScriptStatements(db);
        CreateMissingTables(db, script, existingTables, logger);
        AddMissingColumns(db, existingTables, logger);
        RebuildTablesWithChangedStorage(db, script, existingTables, logger);
    }

    /// <summary>EF's create script split into statements, each tagged with the table it targets.</summary>
    private static List<(string Table, bool IsCreateTable, string Sql)> CreateScriptStatements(DbContext db)
    {
        // Each DDL statement ends with ';' at end-of-line; the bodies contain no semicolons.
        var result = new List<(string, bool, string)>();
        foreach (var sql in Regex.Split(db.Database.GenerateCreateScript(), @";\s*(?:\r?\n|$)").Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (Regex.Match(sql, "^CREATE TABLE \"([^\"]+)\"", RegexOptions.IgnoreCase) is { Success: true } t)
                result.Add((t.Groups[1].Value, true, sql));
            else if (Regex.Match(sql, " ON \"([^\"]+)\"", RegexOptions.IgnoreCase) is { Success: true } i)
                result.Add((i.Groups[1].Value, false, sql));
        }
        return result;
    }

    /// <summary>Executes the statements of EF's create script that target tables not yet in the database.</summary>
    private static void CreateMissingTables(DbContext db, List<(string Table, bool IsCreateTable, string Sql)> script,
                                            HashSet<string> existingTables, ILogger? logger)
    {
        foreach (var (table, _, sql) in script)
        {
            if (existingTables.Contains(table)) continue;
            logger?.LogInformation("Schema upgrade: creating missing object for table {Table}", table);
            db.Database.ExecuteSqlRaw(sql);
        }
    }

    /// <summary>
    /// Rebuilds every table where a mapped column's live storage affinity differs from the model's.
    /// SQLite cannot ALTER a column's type, and keeping e.g. a REAL column for a decimal property
    /// would silently coerce the decimal text back to binary floating point on every write. The
    /// table is recreated from EF's own DDL under a temporary name, rows are copied with their
    /// values converted to the new storage type, the old table is dropped, the new one renamed
    /// and its indexes recreated — all inside one transaction with a foreign-key check.
    /// </summary>
    private static void RebuildTablesWithChangedStorage(DbContext db, List<(string Table, bool IsCreateTable, string Sql)> script,
                                                        HashSet<string> existingTables, ILogger? logger)
    {
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null || !existingTables.Contains(table)) continue;
            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

            var live = LiveColumnTypes(db, table);
            var columns = entity.GetProperties()
                .Select(p => (Property: p, Column: p.GetColumnName(store)))
                .Where(x => x.Column is not null && live.ContainsKey(x.Column!))
                .ToList();
            var changed = columns
                .Where(x => Affinity(live[x.Column!]) != Affinity(x.Property.GetColumnType()))
                .Select(x => x.Column!).ToList();
            if (changed.Count == 0) continue;

            var createTable = script.FirstOrDefault(s => s.IsCreateTable && s.Table.Equals(table, StringComparison.OrdinalIgnoreCase)).Sql;
            if (createTable is null) continue;

            logger?.LogInformation("Schema upgrade: rebuilding {Table} for storage change of {Columns}", table, string.Join(", ", changed));
            var temp = RebuildPrefix + table;
            var connection = db.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) connection.Open();

            var violationsBefore = ForeignKeyViolations(connection, null);
            Execute(connection, null, "PRAGMA foreign_keys = OFF"); // must be set outside a transaction
            try
            {
                using var tx = connection.BeginTransaction();
                Execute(connection, tx, $"DROP TABLE IF EXISTS \"{temp}\"");
                Execute(connection, tx, Regex.Replace(createTable, "^CREATE TABLE \"[^\"]+\"", $"CREATE TABLE \"{temp}\"", RegexOptions.IgnoreCase));
                CopyRows(connection, tx, table, temp, columns);
                Execute(connection, tx, $"DROP TABLE \"{table}\"");
                Execute(connection, tx, $"ALTER TABLE \"{temp}\" RENAME TO \"{table}\"");
                foreach (var (_, _, indexSql) in script.Where(s => !s.IsCreateTable && s.Table.Equals(table, StringComparison.OrdinalIgnoreCase)))
                    Execute(connection, tx, indexSql);

                // only violations introduced by the rebuild count; pre-existing ones must not brick startup
                if (ForeignKeyViolations(connection, tx) > violationsBefore)
                    throw new InvalidOperationException($"Schema upgrade of {table} would break a foreign key; rolled back.");
                tx.Commit();
            }
            finally
            {
                Execute(connection, null, "PRAGMA foreign_keys = ON");
            }
        }
    }

    /// <summary>Copies every row of <paramref name="from"/> into <paramref name="to"/>, converting values to each column's model storage type.</summary>
    private static void CopyRows(IDbConnection connection, IDbTransaction tx, string from, string to,
                                 List<(IProperty Property, string? Column)> columns)
    {
        var names = string.Join(", ", columns.Select(c => $"\"{c.Column}\""));
        var parameters = string.Join(", ", columns.Select((_, i) => $"$p{i}"));

        using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = $"SELECT {names} FROM \"{from}\"";
        using var reader = select.ExecuteReader();

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = $"INSERT INTO \"{to}\" ({names}) VALUES ({parameters})";
        for (var i = 0; i < columns.Count; i++)
        {
            var p = insert.CreateParameter();
            p.ParameterName = $"$p{i}";
            insert.Parameters.Add(p);
        }

        while (reader.Read())
        {
            for (var i = 0; i < columns.Count; i++)
                ((IDataParameter)insert.Parameters[i]!).Value = ToStorage(reader.IsDBNull(i) ? null : reader.GetValue(i), columns[i].Property);
            insert.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Converts a stored value into the property's provider type. For decimal columns a legacy
    /// binary double is converted with <c>(decimal)double</c>, which rounds to 15 significant
    /// digits — the precision the double held — so e.g. 0.1 stays 0.1 rather than 0.1000000000000000055…
    /// </summary>
    private static object ToStorage(object? value, IProperty property)
    {
        if (value is null) return DBNull.Value;
        var provider = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
        provider = Nullable.GetUnderlyingType(provider) ?? provider;
        if (provider != typeof(decimal)) return value;
        return value switch
        {
            double d => (decimal)d,
            float f => (decimal)f,
            long l => (decimal)l,
            string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) => m,
            _ => value
        };
    }

    /// <summary>SQLite's type-affinity rules (https://sqlite.org/datatype3.html §3.1).</summary>
    private static string Affinity(string? declaredType)
    {
        var t = (declaredType ?? "").ToUpperInvariant();
        if (t.Contains("INT")) return "INTEGER";
        if (t.Contains("CHAR") || t.Contains("CLOB") || t.Contains("TEXT")) return "TEXT";
        if (t.Length == 0 || t.Contains("BLOB")) return "BLOB";
        if (t.Contains("REAL") || t.Contains("FLOA") || t.Contains("DOUB")) return "REAL";
        return "NUMERIC";
    }

    private static Dictionary<string, string> LiveColumnTypes(DbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name, type FROM pragma_table_info('{table}')";
        using var reader = command.ExecuteReader();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read()) result[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        return result;
    }

    private static int ForeignKeyViolations(IDbConnection connection, IDbTransaction? tx)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "PRAGMA foreign_key_check";
        using var reader = command.ExecuteReader();
        var count = 0;
        while (reader.Read()) count++;
        return count;
    }

    private static void Execute(IDbConnection connection, IDbTransaction? tx, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>ALTERs existing tables to add any column present in the model but absent in the database.</summary>
    private static void AddMissingColumns(DbContext db, HashSet<string> existingTables, ILogger? logger)
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

    private static List<string> QuerySingleColumn(DbContext db, string sql)
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
