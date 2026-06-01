namespace Client.Application.Models;

/// <summary>The result of auto-detecting a CSV import format (the detected format key and its column headers).</summary>
public class DetectResultDto
{
    public string Format { get; set; } = "unknown";
    public List<string> Headers { get; set; } = [];
}
