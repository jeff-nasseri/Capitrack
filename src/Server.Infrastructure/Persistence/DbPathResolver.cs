using System.Text.Json;

namespace Server.Infrastructure.Persistence;

/// <summary>
/// Resolves the SQLite database path. A persisted settings.json db_path overrides
/// the DB_PATH env default (takes effect on restart).
/// </summary>
public static class DbPathResolver
{
    public static string DefaultPath =>
        Environment.GetEnvironmentVariable("DB_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "data", "capitrack.db");

    public static string SettingsFile =>
        Path.Combine(Path.GetDirectoryName(DefaultPath) ?? ".", "settings.json");

    public static string Resolve()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsFile));
                if (json != null && json.TryGetValue("db_path", out var p) && !string.IsNullOrEmpty(p))
                    return p;
            }
        }
        catch { /* fall back to default */ }
        return DefaultPath;
    }

    public static void Save(string path)
    {
        var dir = Path.GetDirectoryName(SettingsFile);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new { db_path = path }));
    }
}
