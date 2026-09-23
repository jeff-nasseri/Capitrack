namespace Server.Application.Common.Interfaces;

/// <summary>The kind of instrument a symbol is, which decides the providers that can price it.</summary>
public enum AssetClass
{
    /// <summary>A cryptocurrency, written BASE-QUOTE (e.g. BTC-USD).</summary>
    Crypto,
    /// <summary>A stock or ETF ticker (e.g. AAPL, VWCE.DE).</summary>
    Stock,
    /// <summary>A precious metal (e.g. GC=F for gold).</summary>
    Metal,
    /// <summary>A currency pair (e.g. EURUSD=X).</summary>
    Fx
}

/// <summary>One daily closing price: the close of that UTC day for crypto, of that trading day otherwise.</summary>
/// <param name="Date">The day.</param>
/// <param name="Close">The closing price.</param>
public sealed record DailyClose(DateOnly Date, decimal Close);

/// <summary>A provider's daily closes for one symbol, in the currency the provider quotes it.</summary>
/// <param name="Symbol">The Capitrack symbol.</param>
/// <param name="Currency">The currency of every close (e.g. USD; Bitvavo quotes EUR).</param>
/// <param name="Provider">The provider id the closes came from.</param>
/// <param name="Closes">The closes, oldest first.</param>
public sealed record PriceSeries(string Symbol, string Currency, string Provider, IReadOnlyList<DailyClose> Closes);

/// <summary>A price at a moment, and how precise it is.</summary>
/// <param name="Price">The price.</param>
/// <param name="Currency">Its currency.</param>
/// <param name="Provider">The provider id.</param>
/// <param name="Resolution">"hour" (the hourly candle containing the moment) or "day" (the daily close — the documented fallback).</param>
/// <param name="At">The candle or snapshot time the price belongs to.</param>
public sealed record PricePoint(decimal Price, string Currency, string Provider, string Resolution, DateTime At);

/// <summary>What a provider is, what it covers and what it needs.</summary>
/// <param name="Id">A stable id (e.g. "coingecko").</param>
/// <param name="Name">A display name.</param>
/// <param name="Classes">The asset classes it can price.</param>
/// <param name="RequiresApiKey">Whether it only works with an API key.</param>
/// <param name="ApiKeyEnvVar">The environment variable the key is read from, if any.</param>
/// <param name="ApiKeyUrl">Where to get a (free) key, if any.</param>
/// <param name="Website">The provider's website.</param>
/// <param name="Limits">The free tier's limits in plain words.</param>
public sealed record ProviderInfo(string Id, string Name, IReadOnlyList<AssetClass> Classes, bool RequiresApiKey,
    string? ApiKeyEnvVar, string? ApiKeyUrl, string Website, string Limits);

/// <summary>
/// A market-data source. Implementations receive only a symbol and dates — never quantities,
/// accounts or anything else about the portfolio.
/// </summary>
public interface IPriceProvider
{
    /// <summary>Describes the provider.</summary>
    ProviderInfo Info { get; }

    /// <summary>Whether an API key is configured (always true for keyless providers).</summary>
    bool IsConfigured { get; }

    /// <summary>Whether the provider can price this symbol at all (e.g. it knows the coin or market).</summary>
    bool Supports(string symbol, AssetClass assetClass);

    /// <summary>Daily closes for [<paramref name="from"/>, <paramref name="to"/>]; null when it has none. Throws on transport errors.</summary>
    Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>The price in the hour containing <paramref name="utc"/>, when the provider has intraday history; else null.</summary>
    Task<PricePoint?> HourlyAtAsync(string symbol, DateTime utc, CancellationToken ct = default) => Task.FromResult<PricePoint?>(null);

    /// <summary>The latest price, or null.</summary>
    Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default);
}

/// <summary>
/// Routes price requests to the providers selected in settings, per asset class and in order, with
/// fallback when one fails or lacks data. Historical daily closes are cached (they never change).
/// </summary>
public interface IMarketDataService
{
    /// <summary>Daily closes for a symbol, from the first configured provider that has them (or from <paramref name="providerId"/>).</summary>
    Task<PriceSeries?> DailyAsync(string symbol, DateOnly from, DateOnly to, string? providerId = null, CancellationToken ct = default);

    /// <summary>The price at a moment: the hourly candle when available, otherwise that day's close.</summary>
    Task<PricePoint?> PriceAtAsync(string symbol, DateTime utc, string? providerId = null, CancellationToken ct = default);

    /// <summary>The latest quote, with fallback across providers.</summary>
    Task<QuoteDto?> QuoteAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// How many units of <paramref name="to"/> one unit of <paramref name="from"/> is worth on
    /// <paramref name="date"/> (ECB reference rates; the last published rate on or before the date).
    /// </summary>
    Task<decimal?> FxRateAsync(string from, string to, DateOnly date, CancellationToken ct = default);

    /// <summary>Every provider with its configuration state and its position per asset class.</summary>
    Task<List<MarketDataProviderDto>> ProvidersAsync(CancellationToken ct = default);

    /// <summary>Saves which providers are used, and in what order, per asset class.</summary>
    Task SetProviderOrderAsync(Dictionary<AssetClass, List<string>> order, CancellationToken ct = default);
}
