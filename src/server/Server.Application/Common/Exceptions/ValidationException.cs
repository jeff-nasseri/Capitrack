using FluentValidation.Results;

namespace Server.Application.Common.Exceptions;

/// <summary>Thrown by the validation pipeline behavior. Maps to HTTP 400.</summary>
public sealed class ValidationException : Exception
{
    /// <summary>The per-property validation error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Creates an empty validation exception.</summary>
    public ValidationException() : base("One or more validation failures occurred.")
        => Errors = new Dictionary<string, string[]>();

    /// <summary>Creates a validation exception with a single message.</summary>
    public ValidationException(string message) : base(message)
        => Errors = new Dictionary<string, string[]>();

    /// <summary>Creates a validation exception from FluentValidation failures.</summary>
    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
        => Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

    /// <summary>First error message (the original API returns a single { error } string).</summary>
    public string FirstMessage =>
        Errors.SelectMany(e => e.Value).FirstOrDefault() ?? Message;
}
