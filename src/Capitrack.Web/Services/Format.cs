using System.Globalization;

namespace Capitrack.Web.Services;

/// <summary>
/// Number/date formatting — port of modules/utils.js. Uses InvariantCulture
/// (comma grouping, period decimal, English month names) which matches the
/// original 'en-US' Intl output without needing WASM globalization data.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£", ["JPY"] = "¥",
        ["CHF"] = "CHF ", ["CAD"] = "CA$", ["AUD"] = "A$", ["CNY"] = "CN¥",
        ["NZD"] = "NZ$", ["HKD"] = "HK$", ["SGD"] = "SGD ", ["SEK"] = "SEK ",
        ["NOK"] = "NOK ", ["DKK"] = "DKK ", ["PLN"] = "PLN ", ["INR"] = "₹"
    };

    /// <summary>Currency symbol for a code (e.g. USD → "$"). Falls back to "CODE ".</summary>
    public static string Sym(string? currency)
    {
        currency = (currency ?? "USD").ToUpperInvariant();
        return Symbols.TryGetValue(currency, out var s) ? s : currency + " ";
    }

    public static string Money(double? amount, string? currency = "USD") => Money(amount, currency, 2);

    public static string Money(double? amount, string? currency, int decimals)
    {
        if (amount is null) return "--";
        var v = amount.Value;
        var sign = v < 0 ? "-" : "";
        return sign + Sym(currency) + Math.Abs(v).ToString("N" + decimals, Inv);
    }

    /// <summary>Signed money: +$1,500.72 / -$200.00 (sign before symbol, like the V2 design).</summary>
    public static string Signed(double n, string? currency = "USD", int decimals = 2)
        => (n >= 0 ? "+" : "-") + Sym(currency) + Math.Abs(n).ToString("N" + decimals, Inv);

    /// <summary>Signed percent: +61.12% / -19.17%.</summary>
    public static string Pct(double n, int decimals = 2)
        => (n >= 0 ? "+" : "") + n.ToString("N" + decimals, Inv) + "%";

    /// <summary>Grouped fixed-decimal number: 12,345.00.</summary>
    public static string Num(double n, int decimals = 2) => n.ToString("N" + decimals, Inv);

    /// <summary>Share count — 4 decimals for fractional, none for whole.</summary>
    public static string Shares(double n) => n % 1 != 0 ? n.ToString("N4", Inv) : n.ToString("N0", Inv);

    /// <summary>Compact money: $1.5K, €83K. Whole values drop the decimal.</summary>
    public static string CompactMoney(double n, string? currency = null)
    {
        var sym = currency is null ? "" : Sym(currency);
        if (Math.Abs(n) >= 1e6) return sym + (n / 1e6).ToString("0.#", Inv) + "M";
        if (Math.Abs(n) >= 1e3) return sym + (n / 1e3).ToString(n % 1000 == 0 ? "0" : "0.#", Inv) + "K";
        return sym + n.ToString("N0", Inv);
    }

    public static string Number(double? n)
    {
        if (n is null) return "--";
        var v = n.Value;
        if (Math.Abs(v) < 0.01) return v.ToString("F8", Inv);
        if (Math.Abs(v) < 1) return v.ToString("F4", Inv);
        return v.ToString("#,##0.######", Inv);
    }

    public static string Compact(double? n)
    {
        if (n is null || double.IsNaN(n.Value)) return "--";
        var v = n.Value;
        if (Math.Abs(v) >= 1e12) return (v / 1e12).ToString("F1", Inv) + "T";
        if (Math.Abs(v) >= 1e9) return (v / 1e9).ToString("F1", Inv) + "B";
        if (Math.Abs(v) >= 1e6) return (v / 1e6).ToString("F1", Inv) + "M";
        if (Math.Abs(v) >= 1e3) return (v / 1e3).ToString("F1", Inv) + "K";
        return v.ToString("F0", Inv);
    }

    public static string Date(string? d)
    {
        if (string.IsNullOrEmpty(d)) return "--";
        return DateTime.TryParse(d, Inv, DateTimeStyles.None, out var dt)
            ? dt.ToString("MMM d, yyyy", Inv) : "--";
    }
}
