namespace Server.Application.Common.Interfaces;

/// <summary>Password hashing/verification (BCrypt in the infrastructure layer).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    bool Verify(string password, string hash);

    /// <summary>
    /// A valid, throwaway hash to verify against when the account does not exist, so an
    /// unknown-username login costs the same time as a known one (defeats user enumeration).
    /// </summary>
    string DummyHash { get; }
}
