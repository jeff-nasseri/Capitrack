using Server.Application.Common.Interfaces;

namespace Server.Api.Auth;

/// <summary>Resolves the authenticated user from the cookie principal on the current request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? Username => accessor.HttpContext?.User.Identity?.Name;
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
