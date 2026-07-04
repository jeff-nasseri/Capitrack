namespace Client.Application.Models;

/// <summary>An IP address blocked from signing in — either auto-blocked after
/// repeated failures or added manually. A null <see cref="ExpiresAt"/> means the
/// block is permanent.</summary>
public class BlacklistedIpDto
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = "";
    public string? Username { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
