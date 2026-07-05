namespace Client.Application.Models;

/// <summary>The authenticated user's session info (username, base currency, 2FA state and session lifetime).</summary>
public class SessionDto
{
    public string Username { get; set; } = "";
    public string BaseCurrency { get; set; } = "EUR";
    public bool TwoFactorEnabled { get; set; }
    public int SessionLifetimeMinutes { get; set; } = 120;
}
