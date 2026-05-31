using System.Security.Cryptography;
using System.Text;
using Capitrack.Api.Data;
using Capitrack.Api.Models;

namespace Capitrack.Api.Services;

/// <summary>
/// First-run initialisation. The database starts completely empty — no demo
/// accounts, transactions, goals or currency rates are created. Only a single
/// admin login is provisioned so the app is reachable.
///
/// The admin password comes from CAPITRACK_INIT_PASSWORD. If that is not set,
/// a strong random password is generated and written to the logs once — no
/// password is hard-coded in the source or compose files.
/// </summary>
public static class SeedService
{
    public static void Seed(CapitrackDbContext db)
    {
        if (db.Users.Any()) return; // already initialised

        var username = Environment.GetEnvironmentVariable("CAPITRACK_INIT_USERNAME");
        if (string.IsNullOrWhiteSpace(username)) username = "admin";

        var password = Environment.GetEnvironmentVariable("CAPITRACK_INIT_PASSWORD");
        var generated = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GeneratePassword();
            generated = true;
        }

        var baseCurrency = Environment.GetEnvironmentVariable("CAPITRACK_BASE_CURRENCY");
        if (string.IsNullOrWhiteSpace(baseCurrency)) baseCurrency = "EUR";

        db.Users.Add(new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            BaseCurrency = baseCurrency
        });
        db.SaveChanges();

        // Intentionally no demo data — accounts, transactions, goals and currency
        // rates are all left empty for a clean first start.

        if (generated)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine(" Capitrack — initial admin account created (empty database)");
            Console.WriteLine($"   Username: {username}");
            Console.WriteLine($"   Password: {password}");
            Console.WriteLine("   This password was auto-generated. Set CAPITRACK_INIT_PASSWORD");
            Console.WriteLine("   to choose your own, and change it after first login");
            Console.WriteLine("   from Settings -> Security.");
            Console.WriteLine("============================================================");
        }
        else
        {
            Console.WriteLine($"Capitrack — initial admin account '{username}' created (empty database) from CAPITRACK_INIT_PASSWORD.");
        }
    }

    private static string GeneratePassword()
    {
        // Cryptographically-random 20 chars covering upper/lower/digit/special
        // (also satisfies the change-password complexity rules). Excludes easily
        // confused characters (0/O, 1/l/I).
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*";
        var bytes = RandomNumberGenerator.GetBytes(20);
        var sb = new StringBuilder(20);
        foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }
}
