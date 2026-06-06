namespace Server.Application.Settings;

/// <summary>The outcome of a destructive database import, with per-entity counts.</summary>
/// <param name="Accounts">The number of accounts imported.</param>
/// <param name="Transactions">The number of transactions imported.</param>
/// <param name="Tags">The number of tags imported.</param>
/// <param name="Goals">The number of goals imported.</param>
/// <param name="CurrencyRates">The number of FX rates imported.</param>
/// <param name="DailyWealth">The number of daily wealth snapshots imported.</param>
/// <param name="Message">A human-readable summary message.</param>
public record ImportResult(
    int Accounts,
    int Transactions,
    int Tags,
    int Goals,
    int CurrencyRates,
    int DailyWealth,
    string Message);
