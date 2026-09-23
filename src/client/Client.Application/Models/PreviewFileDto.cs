namespace Client.Application.Models;

/// <summary>One uploaded file's parsed preview during a "Check &amp; Import" flow (file name, detected format and the parsed rows).</summary>
public class PreviewFileDto
{
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
    public List<PreviewTransactionDto> Transactions { get; set; } = [];
    public List<RejectedRowDto> Rejected { get; set; } = [];
}

/// <summary>A CSV data row that will not be imported, with the reason.</summary>
public class RejectedRowDto
{
    public int Row { get; set; }
    public string Reason { get; set; } = "";
}
