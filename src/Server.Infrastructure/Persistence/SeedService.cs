using System.Security.Cryptography;
using System.Text;
using Server.Domain.Users;

namespace Server.Infrastructure.Persistence;

/// <summary>
/// First-run initialisation. The database starts empty — only a single admin
/// login is provisioned. The password comes from CAPITRACK_INIT_PASSWORD, or a
/// strong random one is generated and written to the logs once.
/// </summary>
public static class SeedService
{
    public static void Seed(CapitrackDbContext db, IPasswordHasher hasher)
    {
        if (db.Users.Any()) return;

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

        db.Users.Add(User.Create(username, hasher.Hash(password), CurrencyCode.Create(baseCurrency)));
        db.SaveChanges();

        if (generated)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine(" Capitrack - initial admin account created (empty database)");
            Console.WriteLine($"   Username: {username}");
            Console.WriteLine($"   Password: {password}");
            Console.WriteLine("   Auto-generated. Set CAPITRACK_INIT_PASSWORD to choose your own,");
            Console.WriteLine("   and change it after first login from Settings -> Security.");
            Console.WriteLine("============================================================");
        }
        else
        {
            Console.WriteLine($"Capitrack - initial admin account '{username}' created (empty database).");
        }
    }

    private static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*";
        var bytes = RandomNumberGenerator.GetBytes(20);
        var sb = new StringBuilder(20);
        foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
        return sb.ToString();
    }
}
