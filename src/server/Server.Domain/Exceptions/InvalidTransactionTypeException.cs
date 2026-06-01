namespace Server.Domain.Exceptions;

/// <summary>Thrown when a string cannot be parsed into a known transaction type.</summary>
public sealed class InvalidTransactionTypeException : DomainException
{
    /// <summary>Creates the exception for the offending <paramref name="value"/>.</summary>
    public InvalidTransactionTypeException(string value)
        : base($"'{value}' is not a valid transaction type. Allowed: buy, sell, transfer_in, transfer_out, dividend, interest, fee.") { }
}
