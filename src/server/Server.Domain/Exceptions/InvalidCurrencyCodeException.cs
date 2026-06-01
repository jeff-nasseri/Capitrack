namespace Server.Domain.Exceptions;

/// <summary>Thrown when a currency code is empty or otherwise invalid.</summary>
public sealed class InvalidCurrencyCodeException : DomainException
{
    /// <summary>Creates the exception for the offending <paramref name="value"/>.</summary>
    public InvalidCurrencyCodeException(string value)
        : base($"'{value}' is not a valid currency code.") { }
}
