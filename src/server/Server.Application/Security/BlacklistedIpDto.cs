namespace Server.Application.Security;

/// <summary>API representation of a blacklisted IP.</summary>
/// <param name="Id">The record identifier.</param>
/// <param name="IpAddress">The blocked IP.</param>
/// <param name="Username">The associated username (for automatic blocks), if any.</param>
/// <param name="Reason">Why it was blocked.</param>
/// <param name="CreatedAt">When the block was created (UTC).</param>
/// <param name="ExpiresAt">When the block lifts (UTC); null means it never expires (a manual block).</param>
public record BlacklistedIpDto(
    int Id, string IpAddress, string? Username, string Reason, DateTime CreatedAt, DateTime? ExpiresAt);
