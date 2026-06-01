namespace Server.Domain.Exceptions;

/// <summary>Base type for all domain rule violations.</summary>
public class DomainException : Exception
{
    /// <summary>Creates a domain exception with the given message.</summary>
    public DomainException(string message) : base(message) { }
}
