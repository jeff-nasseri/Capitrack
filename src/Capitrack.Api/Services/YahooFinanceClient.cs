using System.Net;
using System.Text.Json;
using Capitrack.Api.Models;

namespace Capitrack.Api.Services;

/// <summary>
/// Minimal Yahoo Finance client replicating the subset of yahoo-finance2 the
/// original app used: quote, chart (history) and search. Uses the cookie+crumb
/// handshake for the v7 quote endpoint, and falls back to the v8 chart endpoint
/// (which needs no crumb) for price if the handshake fails.
/// </summary>
public class YahooFinanceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<YahooFinanceClient> _log;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);
    private string? _crumb;

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";

    public YahooFinanceClient(ILogger<YahooFinanceClient> log)
    {
        _log = log;
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _http.DefaultRequestHeaders.Add("Accept", "application/json,text/plain,*/*");
    }

    private async Task EnsureCrumbAsync()
    {
        if (_crumb != null) return;
        await _crumbLock.WaitAsync();
        try
        {
            if (_crumb != null) return;
            // Hit a Yahoo origin to obtain the session cookie, then request a crumb.
            try { await _http.GetAsync("https://fc.yahoo.com/"); } catch { /* expected 404, sets cookie */ }
            var resp = await _http.GetAsync("https://query2.finance.yahoo.com/v1/test/getcrumb");
            if (resp.IsSuccessStatusCode)
            {
                var crumb = (await resp.Content.ReadAsStringAsync()).Trim();
                if (!string.IsNullOrEmpty(crumb) && !crumb.Contains('<')) _crumb = crumb;
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Yahoo crumb handshake failed"); }
        finally { _crumbLock.Release(); }
    }

    public async Task<QuoteDto?> QuoteAsync(string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        // Primary: v7 quote (needs crumb) → gives name + change percent.
        try
        {
            await EnsureCrumbAsync();
            if (_crumb != null)
            {
                var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(symbol)}&crumb={Uri.EscapeDataString(_crumb)}";
                var resp = await _http.GetAsync(url);
                if (resp.StatusCode == HttpStatusCode.Unauthorized) { _crumb = null; }
                else if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                    var results = doc.RootElement.GetProperty("quoteResponse").GetProperty("result");
                    if (results.GetArrayLength() > 0)
                    {
                        var q = results[0];
                        return new QuoteDto
                        {
                            Symbol = symbol,
                            Price = GetDouble(q, "regularMarketPrice") ?? 0,
                            Currency = GetString(q, "currency") ?? "USD",
                            Name = GetString(q, "shortName") ?? GetString(q, "longName") ?? symbol,
                            ChangePercent = GetDouble(q, "regularMarketChangePercent") ?? 0
                        };
                    }
                }
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Yahoo v7 quote failed for {Symbol}", symbol); }

        // Fallback: v8 chart meta (no crumb required).
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1d";
            var resp = await _http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                var result = doc.RootElement.GetProperty("chart").GetProperty("result");
                if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
                {
                    var meta = result[0].GetProperty("meta");
                    var price = GetDouble(meta, "regularMarketPrice") ?? 0;
                    var prev = GetDouble(meta, "chartPreviousClose") ?? GetDouble(meta, "previousClose") ?? 0;
                    var changePct = prev > 0 ? (price - prev) / prev * 100 : 0;
                    return new QuoteDto
                    {
                        Symbol = symbol,
                        Price = price,
                        Currency = GetString(meta, "currency") ?? "USD",
                        Name = GetString(meta, "shortName") ?? GetString(meta, "longName") ?? symbol,
                        ChangePercent = changePct
                    };
                }
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Yahoo v8 chart-meta fallback failed for {Symbol}", symbol); }

        return null;
    }

    public async Task<List<HistoryPointDto>> ChartAsync(string symbol, DateTime period1, string interval)
    {
        symbol = symbol.ToUpperInvariant();
        var p1 = new DateTimeOffset(DateTime.SpecifyKind(period1, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var p2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?period1={p1}&period2={p2}&interval={interval}";
        var list = new List<HistoryPointDto>();
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return list;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = doc.RootElement.GetProperty("chart").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return list;
        var r0 = result[0];
        if (!r0.TryGetProperty("timestamp", out var ts)) return list;
        var quote = r0.GetProperty("indicators").GetProperty("quote")[0];
        var closes = quote.TryGetProperty("close", out var c) ? c : default;
        var opens = quote.TryGetProperty("open", out var o) ? o : default;
        var highs = quote.TryGetProperty("high", out var h) ? h : default;
        var lows = quote.TryGetProperty("low", out var l) ? l : default;
        var vols = quote.TryGetProperty("volume", out var v) ? v : default;
        for (var i = 0; i < ts.GetArrayLength(); i++)
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(ts[i].GetInt64()).UtcDateTime;
            list.Add(new HistoryPointDto(
                date,
                Index(closes, i), Index(opens, i), Index(highs, i), Index(lows, i), Index(vols, i)));
        }
        return list;
    }

    public async Task<List<SearchResultDto>> SearchAsync(string query)
    {
        var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}";
        var list = new List<SearchResultDto>();
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return list;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("quotes", out var quotes)) return list;
        foreach (var q in quotes.EnumerateArray())
        {
            var symbol = GetString(q, "symbol");
            if (symbol == null) continue;
            list.Add(new SearchResultDto(
                symbol,
                GetString(q, "shortname") ?? GetString(q, "longname") ?? symbol,
                GetString(q, "quoteType"),
                GetString(q, "exchDisp") ?? GetString(q, "exchange")));
        }
        return list;
    }

    private static double? Index(JsonElement arr, int i)
    {
        if (arr.ValueKind != JsonValueKind.Array || i >= arr.GetArrayLength()) return null;
        var el = arr[i];
        return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;
    }

    private static double? GetDouble(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static string? GetString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
