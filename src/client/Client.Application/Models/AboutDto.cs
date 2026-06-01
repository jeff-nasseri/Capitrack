namespace Client.Application.Models;

/// <summary>Application identity shown on the Settings/About panel (product name and version).</summary>
public class AboutDto
{
    public string Name { get; set; } = "Capitrack";
    public string Version { get; set; } = "1.0.0";
}
