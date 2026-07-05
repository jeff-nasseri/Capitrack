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
    /// <summary>Step 1 of login: username + password. Returns a session, or signals that a 2FA code is required.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        // The IP + user agent are taken from the request (via ForwardedHeaders), never from the body,
        // so a client cannot spoof them to dodge the IP blacklist.
        var result = await mediator.Send(command with { IpAddress = ClientIp(), UserAgent = UserAgentOrNull() });
        if (!result.TwoFactorRequired && result.Session is not null)
            await SignInAsync(result.Session);
        return Ok(result);
    }

    /// <summary>Step 2 of login for 2FA-protected accounts: username + password + TOTP code.</summary>
    [HttpPost("login/2fa")]
    public async Task<IActionResult> LoginTwoFactor([FromBody] VerifyTwoFactorLoginCommand command)
    {
        var result = await mediator.Send(command with { IpAddress = ClientIp(), UserAgent = UserAgentOrNull() });
        if (result.Session is not null)
            await SignInAsync(result.Session);
        return Ok(result);
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

    /// <summary>Changes how long sessions stay signed in (15–120 minutes). Applies to live sessions too.</summary>
    [HttpPut("session-lifetime")]
    [Authorize]
    public async Task<IActionResult> ChangeSessionLifetime([FromBody] ChangeSessionLifetimeCommand command)
    {
        await mediator.Send(command);
        return Ok(new { message = "Session lifetime updated" });
    }

    /// <summary>Begins 2FA setup: returns the secret, otpauth URI and a QR SVG to scan.</summary>
    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> SetupTwoFactor() => Ok(await mediator.Send(new StartTwoFactorSetupCommand()));

    /// <summary>Confirms and enables 2FA with a code from the authenticator app.</summary>
    [HttpPost("2fa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorCommand command)
    {
        await mediator.Send(command with { IpAddress = ClientIp(), UserAgent = UserAgentOrNull() });
        return Ok(new { message = "Two-factor authentication enabled" });
    }

    /// <summary>Disables 2FA (requires the account password).</summary>
    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] DisableTwoFactorCommand command)
    {
        await mediator.Send(command with { IpAddress = ClientIp(), UserAgent = UserAgentOrNull() });
        return Ok(new { message = "Two-factor authentication disabled" });
    }

    private async Task SignInAsync(SessionDto session)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, session.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        // The ticket carries the user's configured lifetime; sliding expiration renews it in
        // same-sized windows, so this acts as an inactivity timeout of exactly that length.
        var now = DateTimeOffset.UtcNow;
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                IssuedUtc = now,
                ExpiresUtc = now.AddMinutes(session.SessionLifetimeMinutes)
            });
    }

    private string ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string? UserAgentOrNull() =>
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
}
