using Server.Application.Accounts.Commands;
using Server.Application.Accounts.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await mediator.Send(new GetAccountsQuery()));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => Ok(await mediator.Send(new GetAccountByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountCommand command) =>
        Ok(await mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await mediator.Send(new DeleteAccountCommand(id));
        return Ok(new { message = "Account deleted" });
    }

    [HttpGet("{id:int}/holdings")]
    public async Task<IActionResult> Holdings(int id) => Ok(await mediator.Send(new GetAccountHoldingsQuery(id)));

    [HttpDelete("purge/all")]
    public async Task<IActionResult> Purge()
    {
        await mediator.Send(new PurgeAllAccountsCommand());
        return Ok(new { message = "All accounts, transactions, goals, and cached prices have been purged." });
    }
}
