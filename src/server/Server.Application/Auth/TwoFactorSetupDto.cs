namespace Server.Application.Auth;

/// <summary>The data returned when starting 2FA setup, for the user to add to their authenticator app.</summary>
/// <param name="Secret">The base32 TOTP secret (for manual entry).</param>
/// <param name="OtpauthUri">The otpauth:// URI encoded in the QR code.</param>
/// <param name="QrSvg">A self-contained SVG QR code the user can scan.</param>
public record TwoFactorSetupDto(string Secret, string OtpauthUri, string QrSvg);
