using System.Globalization;
using System.Text.Json;

namespace Server.Infrastructure.Services.MarketData;

/// <summary>Exact decimal reads from provider JSON, whether a price is a number ("97031.5", "1.2e-05") or a string.</summary>
public static class JsonNumbers
{
    public static decimal? ToDecimal(JsonElement e)
    {
        var text = e.ValueKind switch
        {
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.String => e.GetString(),
            _ => null
        };
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}
