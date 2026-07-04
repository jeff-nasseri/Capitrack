using System.Net;
using System.Net.Sockets;
using Server.Domain.Security;

namespace Server.Application.Security.Commands;

/// <summary>Manually adds a (permanent) IP block.</summary>
/// <param name="IpAddress">The IP to block.</param>
/// <param name="Reason">An optional reason.</param>
public record AddBlacklistCommand(string? IpAddress, string? Reason) : IRequest<BlacklistedIpDto>;

/// <summary>Validates <see cref="AddBlacklistCommand"/>.</summary>
public sealed class AddBlacklistValidator : AbstractValidator<AddBlacklistCommand>
{
    /// <summary>Requires a syntactically valid, publicly-routable IP address.</summary>
    public AddBlacklistValidator()
    {
        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("An IP address is required.")
            .Must(ip => IPAddress.TryParse((ip ?? "").Trim(), out _)).WithMessage("Enter a valid IP address.")
            .Must(IsPubliclyRoutable)
                .WithMessage("Only public IP addresses can be blocked — private, loopback and link-local ranges are rejected (blocking them could lock you or the proxy out).");
    }

    /// <summary>
    /// Rejects addresses that must never be blacklisted: loopback, private (RFC 1918 / ULA),
    /// link-local, multicast and unspecified. These are the reverse-proxy / admin ranges, so
    /// blocking them would deny service to every user (or to the operator).
    /// </summary>
    private static bool IsPubliclyRoutable(string? value)
    {
        if (!IPAddress.TryParse((value ?? "").Trim(), out var ip)) return false;   // handled by the parse rule above
        if (IPAddress.IsLoopback(ip)) return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0) return false;                              // 0.0.0.0/8 unspecified
            if (b[0] == 10) return false;                             // 10.0.0.0/8
            if (b[0] == 127) return false;                           // loopback
            if (b[0] == 169 && b[1] == 254) return false;            // 169.254.0.0/16 link-local
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false; // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) return false;            // 192.168.0.0/16
            if (b[0] >= 224) return false;                           // 224+ multicast / reserved
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return false;
            if (ip.Equals(IPAddress.IPv6Loopback) || ip.Equals(IPAddress.IPv6Any)) return false;
            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return false;                 // fc00::/7 unique-local
            return true;
        }

        return false;
    }
}

/// <summary>Handles <see cref="AddBlacklistCommand"/>.</summary>
public sealed class AddBlacklistHandler(
    IBlacklistRepository blacklist,
    IUnitOfWork uow,
    IMapper mapper,
    ILogger<AddBlacklistHandler> logger)
    : IRequestHandler<AddBlacklistCommand, BlacklistedIpDto>
{
    /// <summary>Adds a permanent manual block (idempotent per IP) and returns the resulting DTO.</summary>
    public async Task<BlacklistedIpDto> Handle(AddBlacklistCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", nameof(AddBlacklistCommand));

        var ip = request.IpAddress!.Trim();
        var existing = await blacklist.GetManualByIpAsync(ip, cancellationToken);
        if (existing is not null)
            return mapper.Map<BlacklistedIpDto>(existing);

        var entry = BlacklistedIp.Manual(ip, request.Reason);
        await blacklist.AddAsync(entry, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);
        return mapper.Map<BlacklistedIpDto>(entry);
    }
}
