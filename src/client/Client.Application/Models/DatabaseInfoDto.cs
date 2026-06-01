namespace Client.Application.Models;

/// <summary>Information about the server's database file (its path and whether it currently exists).</summary>
public class DatabaseInfoDto
{
    public string Path { get; set; } = "";
    public bool Exists { get; set; }
}
