namespace Client.Application.Models;

/// <summary>A single recorded sign-in attempt (success or failure) shown in the
/// Login Security panel.</summary>
public class LoginAttemptDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Success { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? UserAgent { get; set; }
}
