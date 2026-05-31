using Server.Domain.Users;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(CapitrackDbContext db) : IUserRepository
{
    public Task<User?> GetFirstAsync(CancellationToken ct = default) =>
        db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync(ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        return Task.CompletedTask;
    }
}
