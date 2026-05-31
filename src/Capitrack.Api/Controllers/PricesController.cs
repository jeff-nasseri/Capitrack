using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/prices")]
public class PricesController(PriceService prices, WealthService wealth, YahooFinanceClient yahoo) : ControllerBase
{
    [HttpGet("quote/{symbol}")]
    public async Task<IActionResult> Quote(string symbol)
    {
        symbol = symbol.ToUpperInvariant();
        var quote = await prices.GetQuoteAsync(symbol);
        if (quote == null)
            return NotFound(new { error = $"Could not fetch price for {symbol}" });
        return Ok(quote);
    }

    [HttpPost("quotes")]
    public async Task<IActionResult> Quotes([FromBody] QuotesRequest body)
    {
        if (body.Symbols == null)
            return BadRequest(new { error = "symbols array required" });

        var results = new Dictionary<string, object?>();
        foreach (var sym in body.Symbols)
        {
            var s = sym.ToUpperInvariant();
            var quote = await prices.GetQuoteAsync(s);
            results[s] = quote ?? (object)new { symbol = s, price = 0, error = "Service unavailable" };
        }
        return Ok(results);
    }

    [HttpGet("history/{symbol}")]
    public async Task<IActionResult> History(string symbol, [FromQuery] string period = "1y")
    {
        symbol = symbol.ToUpperInvariant();
        var ranges = new Dictionary<string, DateTime>
        {
            ["1w"] = DateTime.UtcNow.AddDays(-7),
            ["1m"] = DateTime.UtcNow.AddDays(-30),
            ["3m"] = DateTime.UtcNow.AddDays(-90),
            ["6m"] = DateTime.UtcNow.AddDays(-180),
            ["1y"] = DateTime.UtcNow.AddDays(-365),
            ["5y"] = DateTime.UtcNow.AddDays(-1825),
            ["max"] = new DateTime(2000, 1, 1)
        };
        var period1 = ranges.GetValueOrDefault(period, ranges["1y"]);
        var interval = period == "1w" ? "1h" : period == "1m" ? "1d" : "1wk";

        try
        {
            var data = await yahoo.ChartAsync(symbol, period1, interval);
            return Ok(data.Where(d => d.Close != null));
        }
        catch (Exception e)
        {
            return NotFound(new { error = $"Could not fetch history for {symbol}: {e.Message}" });
        }
    }

    [HttpGet("search/{query}")]
    public async Task<IActionResult> Search(string query)
    {
        try
        {
            return Ok(await yahoo.SearchAsync(query));
        }
        catch (Exception e)
        {
            return StatusCode(500, new { error = e.Message });
        }
    }

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> DashboardSummary()
    {
        try { return Ok(await wealth.DashboardSummaryAsync()); }
        catch (Exception e) { return StatusCode(500, new { error = e.Message }); }
    }

    [HttpGet("portfolio/history")]
    public async Task<IActionResult> PortfolioHistory([FromQuery(Name = "account_id")] int? accountId, [FromQuery] string? period)
    {
        try { return Ok(await wealth.PortfolioHistoryAsync(accountId, period)); }
        catch (Exception e) { return StatusCode(500, new { error = e.Message }); }
    }

    [HttpGet("daily-wealth")]
    public async Task<IActionResult> GetDailyWealth([FromQuery] string? start, [FromQuery] string? end)
    {
        if (string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end))
            return BadRequest(new { error = "start and end dates required (YYYY-MM-DD)" });
        try { return Ok(await wealth.GetDailyWealthAsync(start, end)); }
        catch (Exception e) { return StatusCode(500, new { error = e.Message }); }
    }

    [HttpPost("daily-wealth")]
    public async Task<IActionResult> SaveDailyWealth()
    {
        try { return Ok(await wealth.SaveDailyWealthAsync()); }
        catch (Exception e) { return StatusCode(500, new { error = e.Message }); }
    }
}
