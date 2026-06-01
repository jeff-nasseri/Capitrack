namespace Server.Domain.Common;

/// <summary>Base class for aggregate roots — entities that own domain events and guard invariants.</summary>
/// <typeparam name="TId">The type of the aggregate's identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>The domain events raised by this aggregate since it was loaded.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Creates an aggregate root with the given identifier.</summary>
    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Creates an aggregate root without an identifier (for ORM materialisation).</summary>
    protected AggregateRoot() { }

    /// <summary>Records a domain event to be dispatched after the aggregate is persisted.</summary>
    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clears all pending domain events.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
