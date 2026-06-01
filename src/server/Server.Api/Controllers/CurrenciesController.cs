using Server.Application.Currencies.Commands;
using Server.Application.Currencies.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/currencies")]
public sealed class CurrenciesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new GetCurrencyRatesQuery()));

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertCurrencyRateCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCurrencyRateCommand command) =>
        Ok(await mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await mediator.Send(new DeleteCurrencyRateCommand(id));
        return Ok(new { message = "Rate deleted" });
    }

    [HttpGet("convert")]
    public async Task<IActionResult> Convert(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? amount) =>
        Ok(await mediator.Send(new ConvertCurrencyQuery(from, to, amount)));
}
