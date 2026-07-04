namespace Server.Application.Common.Interfaces;

/// <summary>TOTP (RFC 6238) two-factor helpers: secret generation, the otpauth URI + QR, and code verification.</summary>
public interface ITotpService
{
    /// <summary>Generates a new random base32 TOTP secret.</summary>
    string GenerateSecret();

    /// <summary>Builds the <c>otpauth://</c> URI that an authenticator app scans.</summary>
    string BuildOtpAuthUri(string secret, string account, string issuer);

    /// <summary>Renders an otpauth URI as a self-contained SVG QR code.</summary>
    string GenerateQrSvg(string otpauthUri);

    /// <summary>Verifies a 6-digit TOTP code against the secret, allowing a small clock-skew window.</summary>
    bool VerifyCode(string? secret, string? code);
}
