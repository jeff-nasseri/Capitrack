namespace Server.Infrastructure.Services;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    // Computed once at startup with the same work factor as real hashes, so a Verify against it
    // takes the same time as a real one — used to keep login timing constant when the username
    // is unknown (mitigates user enumeration via a bcrypt-skip timing oracle).
    public string DummyHash { get; } = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 12);

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }
}
