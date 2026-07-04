namespace Client.Application.Models;

/// <summary>The authenticated user's session info (username, base currency and 2FA state).</summary>
public class SessionDto
{
    public string Username { get; set; } = "";
    public string BaseCurrency { get; set; } = "EUR";
    public bool TwoFactorEnabled { get; set; }
}
