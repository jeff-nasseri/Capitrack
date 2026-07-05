using Server.Domain.Security;
using Server.Domain.Users;

namespace Server.Tests;

public class SecurityTests
{
    // ---- User: two-factor lifecycle ----

    [Fact]
    public void Begin_two_factor_setup_stores_secret_but_leaves_it_disabled()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        user.BeginTwoFactorSetup("JBSWY3DPEHPK3PXP");
        user.TwoFactorSecret.Should().Be("JBSWY3DPEHPK3PXP");
        user.TwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public void Begin_two_factor_setup_rejects_an_empty_secret()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        var act = () => user.BeginTwoFactorSetup("  ");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_two_factor_requires_setup_to_have_started()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        var act = user.ConfirmTwoFactor;
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_two_factor_enables_after_setup()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        user.BeginTwoFactorSetup("JBSWY3DPEHPK3PXP");
        user.ConfirmTwoFactor();
        user.TwoFactorEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_two_factor_clears_the_flag_and_secret()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        user.BeginTwoFactorSetup("JBSWY3DPEHPK3PXP");
        user.ConfirmTwoFactor();

        user.DisableTwoFactor();

        user.TwoFactorEnabled.Should().BeFalse();
        user.TwoFactorSecret.Should().BeNull();
    }

    // ---- Session lifetime ----

    [Fact]
    public void Session_lifetime_defaults_to_two_hours()
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        user.SessionLifetimeMinutes.Should().Be(120);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(120)]
    public void Session_lifetime_accepts_values_within_range(int minutes)
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        user.ChangeSessionLifetime(minutes);
        user.SessionLifetimeMinutes.Should().Be(minutes);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(121)]
    [InlineData(1440)]
    public void Session_lifetime_rejects_values_outside_range(int minutes)
    {
        var user = User.Create("admin", "hash", CurrencyCode.Eur);
        var act = () => user.ChangeSessionLifetime(minutes);
        act.Should().Throw<DomainException>();
    }

    // ---- LoginAttempt audit record ----

    [Fact]
    public void Login_attempt_defaults_a_blank_ip_to_unknown()
    {
        var attempt = LoginAttempt.Record("admin", "  ", success: false, "invalid_password", userAgent: null);
        attempt.IpAddress.Should().Be("unknown");
        attempt.UserAgent.Should().BeNull();
        attempt.Success.Should().BeFalse();
    }

    [Fact]
    public void Login_attempt_caps_an_overlong_username()
    {
        var attempt = LoginAttempt.Record(new string('x', 500), "1.2.3.4", success: true, "success", "agent");
        attempt.Username.Length.Should().Be(128);
    }

    // ---- BlacklistedIp ----

    [Fact]
    public void Manual_block_never_expires()
    {
        var block = BlacklistedIp.Manual("203.0.113.7", "spam");
        block.ExpiresAt.Should().BeNull();
        block.IsActive(DateTime.UtcNow.AddYears(10)).Should().BeTrue();
    }

    [Fact]
    public void Temporary_block_is_active_until_it_expires()
    {
        var now = DateTime.UtcNow;
        var block = BlacklistedIp.Temporary("203.0.113.7", "admin", "brute force", now.AddMinutes(15));
        block.IsActive(now.AddMinutes(5)).Should().BeTrue();
        block.IsActive(now.AddMinutes(20)).Should().BeFalse();
        block.Username.Should().Be("admin");
    }
}
