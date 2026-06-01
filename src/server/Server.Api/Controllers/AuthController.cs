using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Server.Application.Auth.Commands;
using Server.Application.Auth.Queries;

namespace Server.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var session = await mediator.Send(command);
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, session.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Ok(session);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out" });
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session() => Ok(await mediator.Send(new GetSessionQuery()));

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        await mediator.Send(command);
        return Ok(new { message = "Password updated" });
    }

    [HttpPut("currency")]
    [Authorize]
    public async Task<IActionResult> ChangeCurrency([FromBody] ChangeBaseCurrencyCommand command)
    {
        await mediator.Send(command);
        return Ok(new { message = "Base currency updated" });
    }
}
