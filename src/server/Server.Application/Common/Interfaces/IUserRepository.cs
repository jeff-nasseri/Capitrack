using Server.Domain.Users;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for the <see cref="User"/> aggregate.</summary>
public interface IUserRepository
{
    /// <summary>Returns the first (lowest-id) user, or null.</summary>
    Task<User?> GetFirstAsync(CancellationToken ct = default);

    /// <summary>Returns the user with the given username, or null.</summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Tracks a new user for insertion.</summary>
    Task AddAsync(User user, CancellationToken ct = default);
}
