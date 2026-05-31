using Server.Domain.Accounts;
using Server.Domain.Currencies;
using Server.Domain.Goals;
using Server.Domain.Tags;
using Server.Domain.Transactions;
using Server.Domain.Users;

namespace Server.Tests;

public class AggregatesTests
{
    [Fact]
    public void Account_requires_a_name()
    {
        var act = () => Account.Create("  ", AccountType.Stock, CurrencyCode.Usd, "", "wallet", Color.Default);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Account_create_defaults_icon_when_blank()
    {
        var account = Account.Create("Main", AccountType.General, CurrencyCode.Eur, "", "", Color.Default);
        account.Icon.Should().Be("wallet");
        account.Name.Should().Be("Main");
    }

    [Fact]
    public void AccountType_is_tolerant_of_unknown_values()
    {
        AccountType.From("crypto").Should().Be(AccountType.Crypto);
        AccountType.From("brokerage").Value.Should().Be("brokerage");
        AccountType.From(null).Should().Be(AccountType.General);
    }

    [Fact]
    public void Transaction_requires_an_account()
    {
        var act = () => Transaction.Create(0, Symbol.Create("AAPL"), TransactionType.Buy,
            Quantity.Create(1), 100, 0, CurrencyCode.Usd, TradeDate.Create("2024-01-01"), null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Transaction_gross_value_is_quantity_times_price()
    {
        var tx = Transaction.Create(1, Symbol.Create("AAPL"), TransactionType.Buy,
            Quantity.Create(10), 150, 1, CurrencyCode.Usd, TradeDate.Create("2024-01-01"), null);
        tx.GrossValue.Amount.Should().Be(1500);
        tx.GrossValue.Currency.Should().Be(CurrencyCode.Usd);
    }

    [Fact]
    public void Tag_requires_a_name()
    {
        var act = () => Tag.Create("", Color.Default);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Goal_requires_a_title()
    {
        var act = () => Goal.Create("", 1000, TradeDate.Create("2030-01-01"), "", false, null);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void User_requires_a_username()
    {
        var act = () => User.Create("", "hash", CurrencyCode.Eur);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void User_can_change_password_and_currency()
    {
        var user = User.Create("admin", "old", CurrencyCode.Eur);
        user.ChangePassword("new");
        user.ChangeBaseCurrency(CurrencyCode.Usd);
        user.PasswordHash.Should().Be("new");
        user.BaseCurrency.Should().Be(CurrencyCode.Usd);
    }

    [Fact]
    public void CurrencyRate_set_rate_updates_value()
    {
        var rate = CurrencyRate.Create(CurrencyCode.Usd, CurrencyCode.Eur, 0.9);
        rate.SetRate(0.95);
        rate.Rate.Should().Be(0.95);
    }
}
