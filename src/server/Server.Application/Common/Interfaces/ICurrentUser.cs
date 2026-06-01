namespace Server.Application.Common.Interfaces;

/// <summary>The currently authenticated user, resolved from the request context.</summary>
public interface ICurrentUser
{
    /// <summary>The signed-in user's name, or null when anonymous.</summary>
    string? Username { get; }

    /// <summary>True when a user is authenticated.</summary>
    bool IsAuthenticated { get; }
}
