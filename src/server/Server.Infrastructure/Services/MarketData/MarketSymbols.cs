namespace Server.Infrastructure.Services.MarketData;

/// <summary>How Capitrack symbols (Yahoo-style) map to asset classes and to what providers call them.</summary>
public static class MarketSymbols
{
    private static readonly HashSet<string> Fiat = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "CHF", "JPY", "CAD", "AUD", "NZD", "SEK", "NOK", "DKK", "PLN", "CZK", "HUF",
        "CNY", "HKD", "SGD", "INR", "BRL", "MXN", "ZAR", "TRY", "KRW"
    };

    /// <summary>Capitrack's metal symbols (Yahoo futures) → ISO 4217 metal codes.</summary>
    private static readonly Dictionary<string, string> Metals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GC=F"] = "XAU", ["SI=F"] = "XAG", ["PL=F"] = "XPT", ["PA=F"] = "XPD"
    };

    /// <summary>The asset class of a symbol.</summary>
    public static AssetClass Classify(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();
        if (Metals.ContainsKey(s)) return AssetClass.Metal;
        if (s.EndsWith("=X", StringComparison.Ordinal)) return AssetClass.Fx;
        return CryptoPair(s) is not null ? AssetClass.Crypto : AssetClass.Stock;
    }

    /// <summary>"BTC-USD" → (BTC, USD); "YF-DAI-USD" → (YF-DAI, USD); a stock like "BRK-B" → null.</summary>
    public static (string Base, string Quote)? CryptoPair(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();
        var dash = s.LastIndexOf('-');
        if (dash <= 0 || dash == s.Length - 1) return null;
        var (b, q) = (s[..dash], s[(dash + 1)..]);
        return Fiat.Contains(q) && !Fiat.Contains(b) ? (b, q) : null;
    }

    /// <summary>"GC=F" → "XAU".</summary>
    public static string? MetalCode(string symbol) => Metals.GetValueOrDefault(symbol.Trim());

    /// <summary>"EURUSD=X" → (EUR, USD).</summary>
    public static (string Base, string Quote)? FxPair(string symbol)
    {
        var s = symbol.Trim().ToUpperInvariant();
        return s.Length == 8 && s.EndsWith("=X", StringComparison.Ordinal) ? (s[..3], s[3..6]) : null;
    }
}

/// <summary>Spaces requests to one provider so its free-tier rate limit is never exceeded.</summary>
public sealed class RateGate(TimeSpan minInterval)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private DateTime _next = DateTime.MinValue;

    /// <summary>Waits until the next request may be sent.</summary>
    public async Task WaitAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var wait = _next - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _next = DateTime.UtcNow + minInterval;
        }
        finally { _lock.Release(); }
    }
}
