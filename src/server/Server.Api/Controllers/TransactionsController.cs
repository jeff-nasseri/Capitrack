using System.Text;
using Server.Application.Transactions.Commands;
using Server.Application.Transactions.Queries;
using Server.Application.Common.Interfaces;

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
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        [FromQuery] int? page,
        [FromQuery(Name = "page_size")] int? pageSize)
    {
        // page/page_size, when valid, take precedence over raw limit/offset.
        if (page is >= 1 && pageSize is >= 1)
        {
            offset = (page.Value - 1) * pageSize.Value;
            limit = pageSize.Value;
        }

        // Always expose the unpaged total so the client can drive pagination.
        var total = await mediator.Send(new GetTransactionsCountQuery(accountId, symbol, search, type));
        Response.Headers.Append("X-Total-Count", total.ToString());

        var items = await mediator.Send(new GetTransactionsQuery(accountId, symbol, search, type, limit, offset));
        return Ok(items);
    }

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

    /// <summary>Imports several CSV files into one account in a single call (aggregate result, dedup across files).</summary>
    [HttpPost("import/csv/bulk")]
    public async Task<IActionResult> ImportBulk(
        [FromForm] IFormFileCollection files,
        [FromForm(Name = "account_id")] int accountId,
        [FromForm] string? format)
    {
        if (files is null || files.Count == 0) return BadRequest(new { error = "No files uploaded" });

        int imported = 0, skipped = 0, total = 0, rejected = 0, updated = 0;
        var errors = new List<string>();
        var rejections = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            using var reader = new StreamReader(file.OpenReadStream());
            var content = await reader.ReadToEndAsync();
            var r = await mediator.Send(new ImportTransactionsCsvCommand(content, accountId, format));
            imported += r.Imported;
            skipped += r.Skipped;
            total += r.Total;
            rejected += r.Rejected;
            updated += r.Updated;
            if (r.Errors is { Count: > 0 }) errors.AddRange(r.Errors.Select(e => $"{file.FileName}: {e}"));
            if (r.Rejections is { Count: > 0 }) rejections.AddRange(r.Rejections.Select(e => $"{file.FileName}: {e}"));
        }
        return Ok(new ImportResultDto(imported, skipped, total, errors, "bulk", rejected, rejections, updated));
    }

    /// <summary>
    /// Previews importing one or more CSV files (nothing is written): every row's status and
    /// transactions, and per-file reconciliation. An optional <c>selection</c> field — a JSON array
    /// aligned with the files, e.g. <c>[{"rows":[1,2],"staked":[2]}]</c> — previews a row selection.
    /// </summary>
    [HttpPost("import/preview")]
    public async Task<IActionResult> ImportPreview(
        [FromForm] IFormFileCollection files,
        [FromForm(Name = "account_id")] int accountId,
        [FromForm] string? selection)
    {
        if (files is null || files.Count == 0) return BadRequest(new { error = "No files uploaded" });
        return Ok(await mediator.Send(new PreviewImportCommand(accountId, await ReadFilesAsync(files, selection))));
    }

    /// <summary>
    /// Imports the rows selected in a Check &amp; Import preview. The same files are sent again with the
    /// same <c>selection</c> field; the server re-parses them and runs the preview's exact plan.
    /// </summary>
    [HttpPost("import/selected")]
    public async Task<IActionResult> ImportSelected(
        [FromForm] IFormFileCollection files,
        [FromForm(Name = "account_id")] int accountId,
        [FromForm] string? selection)
    {
        if (files is null || files.Count == 0) return BadRequest(new { error = "No files uploaded" });
        return Ok(await mediator.Send(new ImportSelectedCommand(accountId, await ReadFilesAsync(files, selection))));
    }

    private static readonly System.Text.Json.JsonSerializerOptions SelectionJson =
        new(System.Text.Json.JsonSerializerDefaults.Web) { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower };

    private static async Task<List<ImportFileInput>> ReadFilesAsync(IFormFileCollection files, string? selection)
    {
        var selections = string.IsNullOrWhiteSpace(selection)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<FileSelectionDto?>>(selection, SelectionJson) ?? [];
        var result = new List<ImportFileInput>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            using var reader = new StreamReader(files[i].OpenReadStream()); // detects and strips a BOM (UTF-8/16)
            result.Add(new ImportFileInput(files[i].FileName, await reader.ReadToEndAsync(), i < selections.Count ? selections[i] : null));
        }
        return result;
    }
}
