namespace Server.Domain.Exceptions;

/// <summary>Thrown when a ticker symbol is empty.</summary>
public sealed class InvalidSymbolException : DomainException
{
    /// <summary>Creates the exception.</summary>
    public InvalidSymbolException() : base("A symbol must not be empty.") { }
}
