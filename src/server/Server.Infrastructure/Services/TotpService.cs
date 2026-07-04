using OtpNet;
using QRCoder;

namespace Server.Infrastructure.Services;

/// <summary>
/// TOTP (RFC 6238) two-factor implementation via Otp.NET, with the QR rendered by QRCoder as
/// a dependency-free SVG. Codes are 6 digits over a 30-second step; verification allows a ±1
/// step of clock skew (the current step plus the adjacent ones, a ~90-second acceptance span).
/// The brute-force IP block, not a narrow window, is what bounds guessing.
/// </summary>
public sealed class TotpService : ITotpService
{
    /// <inheritdoc />
    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    /// <inheritdoc />
    public string BuildOtpAuthUri(string secret, string account, string issuer)
    {
        var label = Uri.EscapeDataString($"{issuer}:{account}");
        var iss = Uri.EscapeDataString(issuer);
        var sec = Uri.EscapeDataString(secret);
        return $"otpauth://totp/{label}?secret={sec}&issuer={iss}&algorithm=SHA1&digits=6&period=30";
    }

    /// <inheritdoc />
    public string GenerateQrSvg(string otpauthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.M);
        return new SvgQRCode(data).GetGraphic(4);
    }

    /// <inheritdoc />
    public bool VerifyCode(string? secret, string? code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;

        var trimmed = code.Trim();
        if (trimmed.Length != 6 || !trimmed.All(char.IsDigit)) return false;

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(trimmed, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
