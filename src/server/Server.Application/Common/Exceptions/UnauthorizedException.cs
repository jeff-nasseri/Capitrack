namespace Server.Application.Common.Exceptions;

/// <summary>Authentication failed / not signed in. Maps to HTTP 401.</summary>
public sealed class UnauthorizedException : Exception
{
    /// <summary>Creates the exception with the given message.</summary>
    public UnauthorizedException(string message) : base(message) { }
}
