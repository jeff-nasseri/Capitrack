namespace Server.Domain.Exceptions;

/// <summary>Thrown when a date string cannot be parsed as yyyy-MM-dd.</summary>
public sealed class InvalidDateException : DomainException
{
    /// <summary>Creates the exception for the offending <paramref name="value"/>.</summary>
    public InvalidDateException(string value)
        : base($"'{value}' is not a valid date (expected yyyy-MM-dd).") { }
}
