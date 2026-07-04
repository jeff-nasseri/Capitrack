using Server.Application.Security.Commands;
using Server.Application.Security.Queries;

namespace Server.Api.Controllers;

/// <summary>Login-security management: the sign-in audit log and the IP blacklist.</summary>
[ApiController]
[Authorize]
[Route("api/security")]
public sealed class SecurityController(IMediator mediator) : ControllerBase
{
    /// <summary>A most-recent-first page of login attempts; the total is returned via X-Total-Count.</summary>
    [HttpGet("login-attempts")]
    public async Task<IActionResult> LoginAttempts([FromQuery] int? page, [FromQuery(Name = "page_size")] int? pageSize)
    {
        var total = await mediator.Send(new GetLoginAttemptsCountQuery());
        Response.Headers.Append("X-Total-Count", total.ToString());
        return Ok(await mediator.Send(new GetLoginAttemptsQuery(page, pageSize)));
    }

    /// <summary>The active (non-expired) IP blacklist entries.</summary>
    [HttpGet("blacklist")]
    public async Task<IActionResult> Blacklist() => Ok(await mediator.Send(new GetBlacklistQuery()));

    /// <summary>Manually blocks an IP (permanent until removed).</summary>
    [HttpPost("blacklist")]
    public async Task<IActionResult> AddBlacklist([FromBody] AddBlacklistCommand command) =>
        StatusCode(StatusCodes.Status201Created, await mediator.Send(command));

    /// <summary>Removes a blacklist entry (unblocking the IP).</summary>
    [HttpDelete("blacklist/{id:int}")]
    public async Task<IActionResult> RemoveBlacklist(int id)
    {
        await mediator.Send(new RemoveBlacklistCommand(id));
        return Ok(new { message = "Blacklist entry removed" });
    }
}
