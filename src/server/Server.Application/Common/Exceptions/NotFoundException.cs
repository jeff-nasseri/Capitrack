namespace Server.Application.Common.Exceptions;

/// <summary>Requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    /// <summary>Creates the exception with the given message.</summary>
    public NotFoundException(string message) : base(message) { }
}
