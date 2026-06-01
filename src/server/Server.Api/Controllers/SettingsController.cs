using Server.Application.Settings.Commands;
using Server.Application.Settings.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Info() => Ok(await mediator.Send(new GetSettingsInfoQuery()));

    [HttpGet("database")]
    public async Task<IActionResult> Database() => Ok(await mediator.Send(new GetDatabaseQuery()));

    [HttpPut("database")]
    public async Task<IActionResult> SetDatabase([FromBody] SetDatabaseCommand command) =>
        Ok(await mediator.Send(command));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh() => Ok(await mediator.Send(new RefreshCommand()));

    [HttpGet("about")]
    public async Task<IActionResult> About() => Ok(await mediator.Send(new GetAboutQuery()));
}
