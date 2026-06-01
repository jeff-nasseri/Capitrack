namespace Server.Application.Common.Interfaces;

/// <summary>Password hashing/verification (BCrypt in the infrastructure layer).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a stored hash.</summary>
    bool Verify(string password, string hash);
}
