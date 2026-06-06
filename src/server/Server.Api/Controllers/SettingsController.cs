using System.Text.Json;
using Server.Application.Settings.Commands;
using Server.Application.Settings.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    // snake_case + indented, so exported backups are readable and round-trip through import.
    private static readonly JsonSerializerOptions BackupJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [HttpGet]
    public async Task<IActionResult> Info() => Ok(await mediator.Send(new GetSettingsInfoQuery()));

    [HttpGet("database")]
    public async Task<IActionResult> Database() => Ok(await mediator.Send(new GetDatabaseQuery()));

    [HttpPut("database")]
    public async Task<IActionResult> SetDatabase([FromBody] SetDatabaseCommand command) =>
        Ok(await mediator.Send(command));

    /// <summary>Downloads a full JSON snapshot of all portfolio data.</summary>
    [HttpGet("database/export")]
    public async Task<IActionResult> ExportDatabase()
    {
        var snapshot = await mediator.Send(new ExportDatabaseQuery());
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, BackupJson);
        var name = $"capitrack-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        return File(bytes, "application/json", name);
    }

    /// <summary>Replaces ALL portfolio data with the contents of an uploaded backup file.</summary>
    [HttpPost("database/import")]
    public async Task<IActionResult> ImportDatabase([FromForm] IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No backup file was uploaded." });

        DatabaseSnapshot? snapshot;
        try
        {
            await using var stream = file.OpenReadStream();
            snapshot = await JsonSerializer.DeserializeAsync<DatabaseSnapshot>(stream, BackupJson);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "The file is not a valid Capitrack backup (could not parse JSON)." });
        }

        if (snapshot is null)
            return BadRequest(new { error = "The file is not a valid Capitrack backup." });

        return Ok(await mediator.Send(new ImportDatabaseCommand(snapshot)));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh() => Ok(await mediator.Send(new RefreshCommand()));

    /// <summary>Lists the market-data providers used for price lookups.</summary>
    [HttpGet("providers")]
    public async Task<IActionResult> Providers() => Ok(await mediator.Send(new GetProvidersQuery()));

    [HttpGet("about")]
    public async Task<IActionResult> About() => Ok(await mediator.Send(new GetAboutQuery()));
}
