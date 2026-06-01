namespace Server.Domain.Exceptions;

/// <summary>Thrown when a color string is not a valid hex color.</summary>
public sealed class InvalidColorException : DomainException
{
    /// <summary>Creates the exception for the offending <paramref name="value"/>.</summary>
    public InvalidColorException(string value)
        : base($"'{value}' is not a valid hex color (expected #rgb or #rrggbb).") { }
}
