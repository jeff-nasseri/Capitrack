using FluentValidation.Results;

namespace Server.Application.Common.Exceptions;

/// <summary>Thrown by the validation pipeline behavior. Maps to HTTP 400.</summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation failures occurred.")
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(string message) : base(message)
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
        => Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

    /// <summary>First error message (the original API returns a single { error } string).</summary>
    public string FirstMessage =>
        Errors.SelectMany(e => e.Value).FirstOrDefault() ?? Message;
}

/// <summary>Requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Business conflict (e.g. duplicate name). Maps to HTTP 400/409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>Authentication failed / not signed in. Maps to HTTP 401.</summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
