using System.Security.Claims;
using System.Text.RegularExpressions;
using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capitrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public partial class AuthController(CapitrackDbContext db) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest body)
    {
        if (string.IsNullOrEmpty(body.Username) || string.IsNullOrEmpty(body.Password))
            return BadRequest(new { error = "Username and password required" });

        var user = db.Users.FirstOrDefault(u => u.Username == body.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(body.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true });

        return Ok(new { username = user.Username, base_currency = user.BaseCurrency });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out" });
    }

    [AllowAnonymous]
    [HttpGet("session")]
    public IActionResult Session()
    {
        var id = UserId;
        if (id == null) return Unauthorized(new { error = "Not authenticated" });
        var user = db.Users.FirstOrDefault(u => u.Id == id);
        if (user == null) return Unauthorized(new { error = "Not authenticated" });
        return Ok(new { username = user.Username, base_currency = user.BaseCurrency });
    }

    [Authorize]
    [HttpPut("password")]
    public IActionResult ChangePassword([FromBody] PasswordRequest body)
    {
        if (string.IsNullOrEmpty(body.CurrentPassword) || string.IsNullOrEmpty(body.NewPassword))
            return BadRequest(new { error = "Current and new password required" });

        var np = body.NewPassword;
        if (np.Length < 8 || !UpperRx().IsMatch(np) || !LowerRx().IsMatch(np) || !DigitRx().IsMatch(np) || !SpecialRx().IsMatch(np))
            return BadRequest(new { error = "Password must be at least 8 characters with uppercase, lowercase, number, and special character" });

        var user = db.Users.FirstOrDefault(u => u.Id == UserId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(body.CurrentPassword, user.PasswordHash))
            return Unauthorized(new { error = "Current password is incorrect" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(np, 12);
        db.SaveChanges();
        return Ok(new { message = "Password updated" });
    }

    [Authorize]
    [HttpPut("currency")]
    public IActionResult ChangeCurrency([FromBody] CurrencyRequest body)
    {
        if (string.IsNullOrEmpty(body.BaseCurrency))
            return BadRequest(new { error = "Base currency required" });
        var user = db.Users.FirstOrDefault(u => u.Id == UserId);
        if (user == null) return Unauthorized(new { error = "Not authenticated" });
        user.BaseCurrency = body.BaseCurrency;
        db.SaveChanges();
        return Ok(new { message = "Base currency updated" });
    }

    private int? UserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [GeneratedRegex("[A-Z]")] private static partial Regex UpperRx();
    [GeneratedRegex("[a-z]")] private static partial Regex LowerRx();
    [GeneratedRegex("[0-9]")] private static partial Regex DigitRx();
    [GeneratedRegex("[!@#$%^&*]")] private static partial Regex SpecialRx();
}
