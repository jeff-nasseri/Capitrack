namespace Client.Application.Models;

/// <summary>The provisioning payload returned when enrolling in two-factor auth
/// (POST /api/auth/2fa/setup): the shared secret, the otpauth:// URI and a
/// ready-to-render QR-code SVG.</summary>
public class TwoFactorSetupDto
{
    /// <summary>The base32 TOTP shared secret (for manual entry).</summary>
    public string Secret { get; set; } = "";

    /// <summary>The otpauth:// provisioning URI encoded by the QR code.</summary>
    public string OtpauthUri { get; set; } = "";

    /// <summary>A complete &lt;svg&gt;…&lt;/svg&gt; string rendering the QR code.</summary>
    public string QrSvg { get; set; } = "";
}
