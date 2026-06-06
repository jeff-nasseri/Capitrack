namespace Server.Application.Settings;

/// <summary>A complete, self-contained snapshot of all portfolio data, used for export/import.</summary>
/// <param name="Version">The snapshot schema version.</param>
/// <param name="ExportedAt">When the snapshot was produced (UTC).</param>
/// <param name="Accounts">All accounts.</param>
/// <param name="Transactions">All transactions.</param>
/// <param name="Tags">All tags.</param>
/// <param name="Goals">All goals.</param>
/// <param name="CurrencyRates">All FX rates.</param>
/// <param name="DailyWealth">All daily wealth snapshots.</param>
public record DatabaseSnapshot(
    int Version,
    DateTime ExportedAt,
    List<SnapshotAccount> Accounts,
    List<SnapshotTransaction> Transactions,
    List<SnapshotTag> Tags,
    List<SnapshotGoal> Goals,
    List<SnapshotCurrencyRate> CurrencyRates,
    List<SnapshotDailyWealth> DailyWealth);
