namespace Server.Domain.Exceptions;

/// <summary>Thrown when a quantity is negative.</summary>
public sealed class NegativeQuantityException : DomainException
{
    /// <summary>Creates the exception for the offending <paramref name="value"/>.</summary>
    public NegativeQuantityException(double value)
        : base($"Quantity must be zero or positive but was {value}.") { }
}
