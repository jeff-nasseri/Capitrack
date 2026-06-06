namespace Server.Application.Settings;

/// <summary>A single FX rate inside a <see cref="DatabaseSnapshot"/>.</summary>
/// <param name="Id">The rate's original identifier (remapped on import).</param>
/// <param name="FromCurrency">The source currency code.</param>
/// <param name="ToCurrency">The target currency code.</param>
/// <param name="Rate">The conversion rate from source to target.</param>
public record SnapshotCurrencyRate(
    int Id,
    string FromCurrency,
    string ToCurrency,
    double Rate);
