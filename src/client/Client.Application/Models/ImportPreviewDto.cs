namespace Client.Application.Models;

/// <summary>The Check &amp; Import preview of one or more files (the server runs the same plan the import will).</summary>
public class ImportPreviewDto
{
    public List<FilePreviewDto> Files { get; set; } = [];
}

/// <summary>One file's preview: every data row and its reconciliation summary.</summary>
public class FilePreviewDto
{
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
    public List<PreviewRowDto> Rows { get; set; } = [];
    public FileSummaryDto Summary { get; set; } = new();
}

/// <summary>One CSV data row: new, update, duplicate, rejected or unselected, and the transactions it produces.</summary>
public class PreviewRowDto
{
    public int Row { get; set; }
    public string Status { get; set; } = "";
    public string? Reason { get; set; }
    public string? Date { get; set; }
    public DateTime? OccurredAt { get; set; }
    public bool CanStake { get; set; }
    public List<PreviewLegDto> Legs { get; set; } = [];
}

/// <summary>One transaction a row produces.</summary>
public class PreviewLegDto
{
    public string Symbol { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "";
}

/// <summary>Rows read = new + duplicates + rejected (+ not selected), counts by type and per-asset balances.</summary>
public class FileSummaryDto
{
    public int RowsRead { get; set; }
    public int New { get; set; }
    public int Duplicates { get; set; }
    public int Updates { get; set; }
    public int Rejected { get; set; }
    public int Unselected { get; set; }
    public Dictionary<string, int> CountsByType { get; set; } = [];
    public List<AssetSummaryDto> Assets { get; set; } = [];
}

/// <summary>One asset's movement in a file and the account balance before and after the import.</summary>
public class AssetSummaryDto
{
    public string Symbol { get; set; } = "";
    public decimal Received { get; set; }
    public decimal Sent { get; set; }
    public decimal Fees { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal ResultingBalance { get; set; }
}

/// <summary>Which rows of one file to import and which outflows are staked (sent as JSON with the files).</summary>
public class FileSelectionDto
{
    public List<int> Rows { get; set; } = [];
    public List<int> Staked { get; set; } = [];
}
