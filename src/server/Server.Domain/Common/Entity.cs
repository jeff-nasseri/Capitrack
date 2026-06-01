namespace Server.Domain.Common;

/// <summary>Base class for entities — identity-based equality.</summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public abstract class Entity<TId> where TId : notnull
{
    /// <summary>The entity's unique identifier.</summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>Creates an entity with the given identifier.</summary>
    protected Entity(TId id) => Id = id;

    /// <summary>Creates an entity without an identifier (for ORM materialisation).</summary>
    protected Entity() { }

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Entity<TId> other
        && other.GetType() == GetType()
        && EqualityComparer<TId>.Default.Equals(other.Id, Id);

    /// <inheritdoc />
    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);

    /// <summary>Identity equality operator.</summary>
    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) => Equals(a, b);

    /// <summary>Identity inequality operator.</summary>
    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !Equals(a, b);
}
