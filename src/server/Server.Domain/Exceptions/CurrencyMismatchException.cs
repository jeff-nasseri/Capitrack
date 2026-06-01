namespace Server.Domain.Exceptions;

/// <summary>Thrown when monetary arithmetic mixes two different currencies.</summary>
public sealed class CurrencyMismatchException : DomainException
{
    /// <summary>Creates the exception for the mismatched <paramref name="left"/> and <paramref name="right"/> currencies.</summary>
    public CurrencyMismatchException(string left, string right)
        : base($"Cannot operate on money in different currencies ({left} vs {right}).") { }
}
