namespace Server.Application.Common.Exceptions;

/// <summary>Business conflict (e.g. duplicate name). Maps to HTTP 400/409.</summary>
public sealed class ConflictException : Exception
{
    /// <summary>Creates the exception with the given message.</summary>
    public ConflictException(string message) : base(message) { }
}
