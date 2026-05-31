using Server.Infrastructure.Persistence;

namespace Server.Infrastructure.Services;

public sealed class SystemService : ISystemService
{
    public string DbPath => DbPathResolver.Resolve();

    public bool DatabaseExists() => File.Exists(DbPath);

    public (string Message, string Path, bool Exists) SetDatabasePath(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        DbPathResolver.Save(path);
        return ("Database path updated. Restart the application for the change to take effect.", path, File.Exists(path));
    }
}
