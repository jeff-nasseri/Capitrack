namespace Server.Application.Transactions;

/// <summary>The Check &amp; Import preview of one or more files, computed by the same plan the import runs.</summary>
/// <param name="Files">One entry per uploaded file, in upload order.</param>
public record ImportPreviewDto(List<FilePreviewDto> Files);

/// <summary>One file's preview: every data row and a reconciliation summary.</summary>
/// <param name="FileName">The uploaded file's name.</param>
/// <param name="Format">The detected format (e.g. "trezor"), or "unknown".</param>
/// <param name="Rows">Every data row, in file order.</param>
/// <param name="Summary">Counts and per-asset totals for this file.</param>
public record FilePreviewDto(string FileName, string Format, List<PreviewRowDto> Rows, FileSummaryDto Summary);

/// <summary>One CSV data row: what importing it does.</summary>
/// <param name="Row">The 1-based data row number within the file.</param>
/// <param name="Status">"new" (adds transactions), "update" (refreshes an imported one), "duplicate" (already imported), "rejected" (not importable) or "unselected".</param>
/// <param name="Reason">Why the row is rejected (or why it is a duplicate); null otherwise.</param>
/// <param name="Date">The UTC date of the row.</param>
/// <param name="OccurredAt">The exact UTC instant, when known.</param>
/// <param name="CanStake">Whether the row has an outflow the user may flag as staked.</param>
/// <param name="Legs">The transactions the row produces (empty when rejected).</param>
public record PreviewRowDto(int Row, string Status, string? Reason, string? Date, DateTime? OccurredAt, bool CanStake, List<PreviewLegDto> Legs);

/// <summary>One transaction a row produces.</summary>
/// <param name="Symbol">The asset symbol.</param>
/// <param name="Type">The Capitrack transaction type (transfer_in, transfer_out, fee, buy, …).</param>
/// <param name="Quantity">The exact quantity.</param>
/// <param name="Price">The unit price at the time (0 when unknown).</param>
/// <param name="Fee">A monetary commission (brokers).</param>
/// <param name="Currency">The currency of price and commission.</param>
/// <param name="Status">"new", "update", "duplicate" or "adopt" (already present from an older import).</param>
public record PreviewLegDto(string Symbol, string Type, decimal Quantity, decimal Price, decimal Fee, string Currency, string Status);

/// <summary>A file's reconciliation: rows read = new + duplicates + rejected (+ unselected), and per-asset effect.</summary>
/// <param name="RowsRead">Data rows in the file.</param>
/// <param name="New">Rows that add transactions.</param>
/// <param name="Duplicates">Rows already imported (including ones refreshed in place).</param>
/// <param name="Updates">Of the duplicates, rows whose already-imported transactions are refreshed (e.g. pending → confirmed).</param>
/// <param name="Rejected">Rows that cannot be imported (see each row's reason).</param>
/// <param name="Unselected">Rows the user left out.</param>
/// <param name="CountsByType">Transactions the file produces, by type.</param>
/// <param name="Assets">Per-asset totals and balances.</param>
public record FileSummaryDto(int RowsRead, int New, int Duplicates, int Updates, int Rejected, int Unselected,
    Dictionary<string, int> CountsByType, List<AssetSummaryDto> Assets);

/// <summary>One asset's movement in a file and the account balance before and after importing it.</summary>
/// <param name="Symbol">The asset symbol.</param>
/// <param name="Received">Units received / bought in the file.</param>
/// <param name="Sent">Units sent / sold in the file.</param>
/// <param name="Fees">Units spent on network fees in the file.</param>
/// <param name="CurrentBalance">The account's balance now.</param>
/// <param name="ResultingBalance">The account's balance after this import (all files of the batch).</param>
public record AssetSummaryDto(string Symbol, decimal Received, decimal Sent, decimal Fees, decimal CurrentBalance, decimal ResultingBalance);

/// <summary>Which rows of one uploaded file to import, and which of their outflows are staked.</summary>
/// <param name="Rows">The 1-based data row numbers to import.</param>
/// <param name="Staked">Row numbers whose outflow is staked (still part of the holding).</param>
public record FileSelectionDto(List<int> Rows, List<int>? Staked);
