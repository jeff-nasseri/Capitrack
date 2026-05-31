using System.Text;
using Server.Application.Transactions.Commands;
using Server.Application.Transactions.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery(Name = "account_id")] int? accountId,
        [FromQuery] string? symbol,
        [FromQuery] int? limit,
        [FromQuery] int? offset) =>
        Ok(await mediator.Send(new GetTransactionsQuery(accountId, symbol, limit, offset)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) => Ok(await mediator.Send(new GetTransactionByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransactionCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTransactionCommand command) =>
        Ok(await mediator.Send(command with { Id = id }));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await mediator.Send(new DeleteTransactionCommand(id));
        return Ok(new { message = "Transaction deleted" });
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> Export([FromQuery(Name = "account_id")] int? accountId)
    {
        var csv = await mediator.Send(new ExportTransactionsCsvQuery(accountId));
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "transactions.csv");
    }

    [HttpPost("import/csv")]
    public async Task<IActionResult> Import(
        [FromForm] IFormFile? file,
        [FromForm(Name = "account_id")] int accountId,
        [FromForm] string? format)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file uploaded" });
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        return Ok(await mediator.Send(new ImportTransactionsCsvCommand(content, accountId, format)));
    }

    [HttpPost("import/detect")]
    public async Task<IActionResult> Detect([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file uploaded" });
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        return Ok(await mediator.Send(new DetectCsvQuery(content)));
    }
}
