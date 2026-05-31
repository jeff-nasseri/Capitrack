namespace Server.Domain.Exceptions;

/// <summary>Base type for all domain rule violations.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class InvalidTransactionTypeException : DomainException
{
    public InvalidTransactionTypeException(string value)
        : base($"'{value}' is not a valid transaction type. Allowed: buy, sell, transfer_in, transfer_out, dividend, interest, fee.") { }
}

public sealed class InvalidCurrencyCodeException : DomainException
{
    public InvalidCurrencyCodeException(string value)
        : base($"'{value}' is not a valid currency code.") { }
}

public sealed class CurrencyMismatchException : DomainException
{
    public CurrencyMismatchException(string left, string right)
        : base($"Cannot operate on money in different currencies ({left} vs {right}).") { }
}

public sealed class InvalidSymbolException : DomainException
{
    public InvalidSymbolException() : base("A symbol must not be empty.") { }
}

public sealed class NegativeQuantityException : DomainException
{
    public NegativeQuantityException(double value)
        : base($"Quantity must be zero or positive but was {value}.") { }
}

public sealed class InvalidColorException : DomainException
{
    public InvalidColorException(string value)
        : base($"'{value}' is not a valid hex color (expected #rgb or #rrggbb).") { }
}

public sealed class InvalidDateException : DomainException
{
    public InvalidDateException(string value)
        : base($"'{value}' is not a valid date (expected yyyy-MM-dd).") { }
}
