namespace Server.Application.Common.Exceptions;

/// <summary>Too many attempts / the client is rate-limited or blocked. Maps to HTTP 429.</summary>
public sealed class TooManyRequestsException : Exception
{
    /// <summary>Creates the exception with the given message.</summary>
    public TooManyRequestsException(string message) : base(message) { }
}
