namespace Client.Application.Models;

/// <summary>The outcome of a CSV transaction import (counts of imported/skipped rows, detected format and any errors).</summary>
public class ImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Total { get; set; }
    public List<string> Errors { get; set; } = [];
    public string Format { get; set; } = "";
    public string? Error { get; set; }
}
