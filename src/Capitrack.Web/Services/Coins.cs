using System.Globalization;

namespace Capitrack.Web.Services;

/// <summary>Asset "coin" colors/initials and a hex shade helper — ported from the V2 design.</summary>
public static class Coins
{
    private record CoinDef(string Color, string Text);

    private static readonly Dictionary<string, CoinDef> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC-USD"] = new("#f7931a", "B"), ["GC=F"] = new("#eab308", "GC"), ["ETH-USD"] = new("#6481e7", "E"),
        ["AAPL"] = new("#6366f1", "AA"), ["MSFT"] = new("#06b6d4", "MS"), ["NVDA"] = new("#f5b300", "NV"),
        ["AMZN"] = new("#7c83ff", "AM"), ["GOOGL"] = new("#5b8def", "GO"), ["TSLA"] = new("#e6473a", "TS"),
        ["SI=F"] = new("#9aa3b2", "SI"), ["PL=F"] = new("#a3a8bd", "PL"), ["PA=F"] = new("#ced0dd", "PA"),
        ["LTC-USD"] = new("#345d9d", "L"), ["XRP-USD"] = new("#23292f", "X"), ["DOGE-USD"] = new("#c2a633", "D"),
        ["SOL-USD"] = new("#9945ff", "S"), ["ADA-USD"] = new("#0d1e30", "A"), ["BNB-USD"] = new("#f3ba2f", "BN"),
    };

    private static readonly string[] Palette =
        ["#6366f1", "#8b5cf6", "#a855f7", "#ec4899", "#f43f5e", "#f97316", "#eab308", "#22c55e", "#14b8a6", "#06b6d4", "#3b82f6"];

    public static (string Color, string Text) For(string sym)
    {
        if (Map.TryGetValue(sym, out var d)) return (d.Color, d.Text);
        return (HashColor(sym), Initials(sym));
    }

    public static string Color(string sym) => For(sym).Color;

    private static string Initials(string sym)
    {
        var c = sym;
        if (c.EndsWith("-USD", StringComparison.OrdinalIgnoreCase)) c = c[..^4];
        else if (c.EndsWith("=F", StringComparison.OrdinalIgnoreCase)) c = c[..^2];
        return (c.Length >= 2 ? c[..2] : c).ToUpperInvariant();
    }

    private static string HashColor(string s)
    {
        int h = 0;
        foreach (var c in s) unchecked { h = c + ((h << 5) - h); }
        return Palette[Math.Abs((long)h) % Palette.Length];
    }

    /// <summary>Darken/lighten a #rrggbb hex by an amount (e.g. -18).</summary>
    public static string Shade(string hex, int amt)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7) return hex;
        int n = int.Parse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int r = Math.Clamp((n >> 16) + amt, 0, 255);
        int g = Math.Clamp(((n >> 8) & 255) + amt, 0, 255);
        int b = Math.Clamp((n & 255) + amt, 0, 255);
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
