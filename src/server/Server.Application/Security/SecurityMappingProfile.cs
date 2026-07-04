using Server.Domain.Security;

namespace Server.Application.Security;

/// <summary>AutoMapper profile mapping the security entities to their DTOs.</summary>
public sealed class SecurityMappingProfile : Profile
{
    /// <summary>Configures the login-attempt and blacklist mappings (property names line up directly).</summary>
    public SecurityMappingProfile()
    {
        CreateMap<LoginAttempt, LoginAttemptDto>();
        CreateMap<BlacklistedIp, BlacklistedIpDto>();
    }
}
