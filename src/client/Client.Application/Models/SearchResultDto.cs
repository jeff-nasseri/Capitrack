namespace Client.Application.Models;

/// <summary>A single symbol-search match (symbol, name and optional type/exchange).</summary>
public class SearchResultDto
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Type { get; set; }
    public string? Exchange { get; set; }
}
