namespace Server.Application.Auth;

/// <summary>
/// The outcome of a sign-in step: either the second factor is still required, or the user
/// is fully authenticated and a <see cref="SessionDto"/> is returned.
/// </summary>
/// <param name="TwoFactorRequired">True when the password was correct but a TOTP code is still needed.</param>
/// <param name="Session">The session — present only once fully signed in.</param>
public record LoginResultDto(bool TwoFactorRequired, SessionDto? Session)
{
    /// <summary>Password accepted, but a TOTP code is required to complete sign-in.</summary>
    public static LoginResultDto Requires2Fa() => new(true, null);

    /// <summary>Fully signed in.</summary>
    public static LoginResultDto SignedIn(SessionDto session) => new(false, session);
}
