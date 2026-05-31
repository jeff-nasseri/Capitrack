using Capitrack.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Tests;

/// <summary>Spins up an isolated in-memory SQLite CapitrackDbContext for a test.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    public CapitrackDbContext Db { get; }

    public TestDb()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<CapitrackDbContext>().UseSqlite(_conn).Options;
        Db = new CapitrackDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }
}
