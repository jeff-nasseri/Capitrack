using Server.Application.Prices.Commands;
using Server.Application.Prices.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/prices")]
public sealed class PricesController(IMediator mediator) : ControllerBase
{
    [HttpGet("quote/{symbol}")]
    public async Task<IActionResult> Quote(string symbol) => Ok(await mediator.Send(new GetQuoteQuery(symbol)));

    [HttpPost("quotes")]
    public async Task<IActionResult> Quotes([FromBody] GetQuotesQuery query) => Ok(await mediator.Send(query));

    [HttpGet("history/{symbol}")]
    public async Task<IActionResult> History(string symbol, [FromQuery] string? period) =>
        Ok(await mediator.Send(new GetHistoryQuery(symbol, period)));

    [HttpGet("search/{query}")]
    public async Task<IActionResult> Search(string query) => Ok(await mediator.Send(new SearchSymbolsQuery(query)));

    /// <summary>Today's rates to the base currency for the given comma-separated currencies (manual rate, else ECB).</summary>
    [HttpGet("rates")]
    public async Task<IActionResult> Rates([FromQuery] string? currencies) =>
        Ok(await mediator.Send(new GetExchangeRatesQuery(
            (currencies ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(50).ToList())));

    [HttpGet("dashboard/summary")]
    public async Task<IActionResult> Summary() => Ok(await mediator.Send(new GetDashboardSummaryQuery()));

    [HttpGet("portfolio/history")]
    public async Task<IActionResult> Portfolio(
        [FromQuery(Name = "account_id")] int? accountId,
        [FromQuery] string? period) =>
        Ok(await mediator.Send(new GetPortfolioHistoryQuery(accountId, period)));

    [HttpGet("daily-wealth")]
    public async Task<IActionResult> DailyWealth([FromQuery] string? start, [FromQuery] string? end) =>
        Ok(await mediator.Send(new GetDailyWealthQuery(start, end)));

    [HttpPost("daily-wealth")]
    public async Task<IActionResult> SaveDailyWealth() => Ok(await mediator.Send(new SaveDailyWealthCommand()));
}
