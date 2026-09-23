using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Server.Domain.Accounts;
using Server.Infrastructure.Persistence;

namespace Server.Tests;

/// <summary>An in-memory SQLite Capitrack database with the real EF model, for importer/holdings tests.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<CapitrackDbContext> Options { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        Options = new DbContextOptionsBuilder<CapitrackDbContext>().UseSqlite(_connection).Options;
        using var db = NewContext();
        db.Database.EnsureCreated();
    }

    public CapitrackDbContext NewContext() => new(Options);

    public int AddAccount(string name = "Wallet", string type = "crypto", string currency = "USD")
    {
        using var db = NewContext();
        var account = Account.Create(name, AccountType.From(type), CurrencyCode.Create(currency), "", "", Color.Default);
        db.Accounts.Add(account);
        db.SaveChanges();
        return account.Id;
    }

    public void Dispose() => _connection.Dispose();
}
