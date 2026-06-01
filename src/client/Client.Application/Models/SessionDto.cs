namespace Client.Application.Models;

/// <summary>The authenticated user's session info (username and base currency).</summary>
public class SessionDto
{
    public string Username { get; set; } = "";
    public string BaseCurrency { get; set; } = "EUR";
}
