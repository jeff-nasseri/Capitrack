namespace Server.Domain.Common;

/// <summary>Base class for value objects — structural (component-based) equality.</summary>
public abstract class ValueObject
{
    /// <summary>Yields the components that participate in equality and hashing.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc />
    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(0, (hash, component) => HashCode.Combine(hash, component));

    /// <summary>Structural equality operator.</summary>
    public static bool operator ==(ValueObject? a, ValueObject? b) => Equals(a, b);

    /// <summary>Structural inequality operator.</summary>
    public static bool operator !=(ValueObject? a, ValueObject? b) => !Equals(a, b);
}
