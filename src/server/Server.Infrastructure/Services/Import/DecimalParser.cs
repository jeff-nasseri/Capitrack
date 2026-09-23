using System.Globalization;
using System.Text;

namespace Server.Infrastructure.Services.Import;

/// <summary>Which character a file uses as its decimal separator.</summary>
public enum DecimalSeparator
{
    /// <summary>Decide per value (only safe when a value is unambiguous).</summary>
    Auto,
    /// <summary>"1,234.56" — point decimals, comma (or space/apostrophe) grouping.</summary>
    Point,
    /// <summary>"1.234,56" — comma decimals, point (or space/apostrophe) grouping.</summary>
    Comma
}

/// <summary>
/// Exact, culture-tolerant number parsing for CSV imports. Exports differ by locale ("1,284.75"
/// vs "1.284,75" vs "0,5"), may carry currency symbols or codes ("$1,234", "USD 150.23"), use
/// space/NBSP/apostrophe grouping, accounting negatives "(12.5)", a Unicode minus or exponents.
/// A single value like "1,305" is ambiguous on its own, so callers detect the separator once per
/// file from all its numeric values (<see cref="Detect"/>) and parse every value with it.
/// </summary>
public static class DecimalParser
{
    /// <summary>Infers a file's decimal separator from sample values; defaults to <see cref="DecimalSeparator.Point"/>.</summary>
    public static DecimalSeparator Detect(IEnumerable<string?> samples)
    {
        int point = 0, comma = 0;
        foreach (var raw in samples)
        {
            var s = StripNoise(raw ?? "");
            int dots = s.Count(c => c == '.'), commas = s.Count(c => c == ',');
            if (dots > 0 && commas > 0)
            {
                if (s.LastIndexOf('.') > s.LastIndexOf(',')) point++; else comma++;
            }
            else if (commas > 1) point++;               // "1,234,567" → comma is grouping
            else if (dots > 1) comma++;                 // "1.234.567" → point is grouping
            else if (commas == 1 && DigitsAfter(s, ',') != 3) comma++;   // "0,5"
            else if (dots == 1 && DigitsAfter(s, '.') != 3) point++;     // "0.5"
        }
        return comma > point ? DecimalSeparator.Comma : DecimalSeparator.Point;
    }

    /// <summary>Parses <paramref name="text"/> exactly; false when it is not a number (e.g. "ID 0", "", "n/a").</summary>
    public static bool TryParse(string? text, DecimalSeparator separator, out decimal value)
    {
        value = 0;
        var s = StripNoise(text ?? "");
        if (s.Length == 0) return false;

        var negative = false;
        if (s.StartsWith('(') && s.EndsWith(')')) { negative = true; s = s[1..^1]; }
        if (s.Length == 0 || s.Any(c => !(char.IsAsciiDigit(c) || c is '.' or ',' or '-' or '+' or 'e' or 'E'))) return false;

        int dots = s.Count(c => c == '.'), commas = s.Count(c => c == ',');
        if (dots > 0 && commas > 0)
        {
            // the right-most separator is the decimal one, the other is grouping
            s = s.LastIndexOf('.') > s.LastIndexOf(',') ? s.Replace(",", "") : s.Replace(".", "").Replace(',', '.');
        }
        else if (commas > 0)
        {
            var decimalComma = separator switch
            {
                DecimalSeparator.Comma => commas == 1,
                DecimalSeparator.Point => false,
                _ => commas == 1 && DigitsAfter(s, ',') != 3
            };
            s = decimalComma ? s.Replace(',', '.') : s.Replace(",", "");
        }
        else if (dots > 1 || (dots == 1 && separator == DecimalSeparator.Comma && DigitsAfter(s, '.') == 3))
        {
            s = s.Replace(".", ""); // "1.234.567" / "1.305" in a comma-decimal file → grouping
        }

        if (!decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture, out value))
            return false;
        if (negative) value = -value;
        return true;
    }

    /// <summary>Removes whitespace/grouping characters, currency symbols and currency/unit words, and normalises the minus sign.</summary>
    private static string StripNoise(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw.Trim().Trim('"'))
        {
            if (c is '\u2212' or '\u2013') { sb.Append('-'); continue; }                  // Unicode minus / en dash
            if (char.IsWhiteSpace(c) || c is '\u00A0' or '\u202F' or '\u2009' or '\'' or '\u2019' or '\uFEFF') continue;
            if (char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol) continue;   // $ € £ ¥ ₿ …
            sb.Append(c);
        }
        // drop currency codes / unit words ("USD", "BTC", "EUR"), keeping a lone exponent 'E' between digits
        var s = sb.ToString();
        var cleaned = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsLetter(c))
            {
                var exponent = (c is 'e' or 'E') && i > 0 && char.IsAsciiDigit(s[i - 1]) && i + 1 < s.Length
                               && (char.IsAsciiDigit(s[i + 1]) || s[i + 1] is '-' or '+');
                if (!exponent)
                {
                    var j = i;
                    while (j < s.Length && char.IsLetter(s[j])) j++;
                    // only a currency/ticker code (3-6 capitals: USD, EUR, BTC, USDC) is noise; anything
                    // else ("ID 0" for an NFT, "n/a") makes the value non-numeric rather than silently 0
                    var word = s[i..j];
                    if (word.Length is >= 3 and <= 6 && word.All(char.IsAsciiLetterUpper)) { i = j - 1; continue; }
                }
            }
            cleaned.Append(c);
        }
        return cleaned.ToString();
    }

    private static int DigitsAfter(string s, char separator)
    {
        var idx = s.LastIndexOf(separator);
        var n = 0;
        for (var i = idx + 1; i < s.Length && char.IsAsciiDigit(s[i]); i++) n++;
        return n;
    }
}
