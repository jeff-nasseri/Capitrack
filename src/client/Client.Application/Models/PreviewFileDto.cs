namespace Client.Application.Models;

/// <summary>One uploaded file's parsed preview during a "Check &amp; Import" flow (file name, detected format and the parsed rows).</summary>
public class PreviewFileDto
{
    public string FileName { get; set; } = "";
    public string Format { get; set; } = "";
    public List<PreviewTransactionDto> Transactions { get; set; } = [];
}
