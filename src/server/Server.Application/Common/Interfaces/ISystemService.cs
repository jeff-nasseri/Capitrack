namespace Server.Application.Common.Interfaces;

/// <summary>Database path/state and persistence-related settings.</summary>
public interface ISystemService
{
    /// <summary>The resolved database file path.</summary>
    string DbPath { get; }

    /// <summary>Returns true when the database file exists.</summary>
    bool DatabaseExists();

    /// <summary>Sets the database path and returns the resulting message, path and existence flag.</summary>
    (string Message, string Path, bool Exists) SetDatabasePath(string path);
}
