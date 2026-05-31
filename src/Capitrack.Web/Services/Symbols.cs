using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Capitrack.Web.Services;

/// <summary>Symbol icons/colors — port of modules/utils.js (getSymbolColor/Initials/Icon).</summary>
public static partial class Symbols
{
    private static readonly string[] Colors =
    [
        "#6366f1", "#8b5cf6", "#a855f7", "#d946ef", "#ec4899",
        "#f43f5e", "#ef4444", "#f97316", "#f59e0b", "#eab308",
        "#84cc16", "#22c55e", "#14b8a6", "#06b6d4", "#0ea5e9",
        "#3b82f6", "#6366f1", "#8b5cf6"
    ];

    private static readonly Dictionary<string, string> CryptoIcons = new()
    {
        ["BTC-USD"] = "btc", ["ETH-USD"] = "eth", ["LTC-USD"] = "ltc", ["XRP-USD"] = "xrp",
        ["ADA-USD"] = "ada", ["DOT-USD"] = "dot", ["DOGE-USD"] = "doge", ["SOL-USD"] = "sol",
        ["AVAX-USD"] = "avax", ["MATIC-USD"] = "matic", ["LINK-USD"] = "link", ["UNI-USD"] = "uni",
        ["ATOM-USD"] = "atom", ["ALGO-USD"] = "algo", ["XLM-USD"] = "xlm", ["VET-USD"] = "vet",
        ["FIL-USD"] = "fil", ["AAVE-USD"] = "aave", ["MANA-USD"] = "mana", ["SAND-USD"] = "sand",
        ["AXS-USD"] = "axs", ["THETA-USD"] = "theta", ["FTM-USD"] = "ftm", ["NEAR-USD"] = "near",
        ["GRT-USD"] = "grt", ["ENJ-USD"] = "enj", ["BAT-USD"] = "bat", ["CRV-USD"] = "crv",
        ["COMP-USD"] = "comp", ["SNX-USD"] = "snx", ["SUSHI-USD"] = "sushi", ["YFI-USD"] = "yfi",
        ["MKR-USD"] = "mkr", ["ZRX-USD"] = "zrx", ["BNB-USD"] = "bnb", ["TRX-USD"] = "trx",
        ["EOS-USD"] = "eos", ["XTZ-USD"] = "xtz", ["NEO-USD"] = "neo", ["DASH-USD"] = "dash",
        ["ZEC-USD"] = "zec", ["ETC-USD"] = "etc", ["BCH-USD"] = "bch", ["SHIB-USD"] = "shib",
        ["APE-USD"] = "ape", ["CRO-USD"] = "cro", ["LDO-USD"] = "ldo", ["ARB-USD"] = "arb",
        ["OP-USD"] = "op", ["SUI-USD"] = "sui", ["PEPE-USD"] = "pepe", ["IMX-USD"] = "imx",
        ["INJ-USD"] = "inj", ["SEI-USD"] = "sei", ["TIA-USD"] = "tia", ["JUP-USD"] = "jup",
        ["RENDER-USD"] = "rndr", ["FET-USD"] = "fet", ["RUNE-USD"] = "rune", ["STX-USD"] = "stx"
    };

    private record Commodity(string Icon, string Color);
    private static readonly Dictionary<string, Commodity> CommodityIcons = new()
    {
        ["GC=F"] = new("fa-coins", "#FFD700"),
        ["SI=F"] = new("fa-coins", "#C0C0C0"),
        ["PL=F"] = new("fa-coins", "#E5E4E2"),
        ["PA=F"] = new("fa-coins", "#CED0DD"),
        ["CL=F"] = new("fa-oil-can", "#333"),
        ["NG=F"] = new("fa-fire-flame-simple", "#FF6B35")
    };

    public static string Color(string symbol)
    {
        int hash = 0;
        foreach (var c in symbol) unchecked { hash = c + ((hash << 5) - hash); }
        return Colors[Math.Abs((long)hash) % Colors.Length];
    }

    public static string Initials(string symbol)
    {
        var clean = SuffixRx().Replace(symbol, "");
        return (clean.Length >= 2 ? clean[..2] : clean).ToUpperInvariant();
    }

    public static MarkupString Icon(string symbol) => new(BuildIcon(symbol, ""));
    public static MarkupString IconSmall(string symbol) => new(BuildIcon(symbol, "width:24px;height:24px;font-size:0.5rem;"));

    private static string BuildIcon(string symbol, string extra)
    {
        var color = Color(symbol);
        var initials = Initials(symbol);
        var upper = symbol.ToUpperInvariant();

        if (CommodityIcons.TryGetValue(upper, out var commodity))
        {
            var iconSize = extra.Length > 0 ? "0.5rem" : "0.75rem";
            return $"<div class=\"symbol-icon\" style=\"background:{commodity.Color};{extra}\"><i class=\"fas {commodity.Icon}\" style=\"font-size:{iconSize};color:#fff;\"></i></div>";
        }

        if (CryptoIcons.TryGetValue(upper, out var id))
        {
            var url = $"https://cdn.jsdelivr.net/gh/spothq/cryptocurrency-icons@master/svg/color/{id}.svg";
            return $"<div class=\"symbol-icon symbol-icon-img\" style=\"background:{color};{extra}\" data-symbol=\"{upper}\"><img src=\"{url}\" alt=\"{initials}\" onerror=\"this.style.display='none';this.parentElement.classList.remove('symbol-icon-img');this.parentElement.innerHTML='{initials}';\"><span class=\"symbol-icon-fallback\">{initials}</span></div>";
        }

        return $"<div class=\"symbol-icon\" style=\"background:{color};{extra}\">{initials}</div>";
    }

    [GeneratedRegex("(-USD$)|(=F$)")]
    private static partial Regex SuffixRx();
}
