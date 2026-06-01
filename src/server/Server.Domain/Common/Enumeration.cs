namespace Server.Domain.Common;

/// <summary>Base class for type-safe smart enumerations.</summary>
public abstract class Enumeration : IComparable
{
    /// <summary>The display name of the enumeration value.</summary>
    public string Name { get; }

    /// <summary>The numeric identifier of the enumeration value.</summary>
    public int Id { get; }

    /// <summary>Creates an enumeration value with the given identifier and name.</summary>
    protected Enumeration(int id, string name) => (Id, Name) = (id, name);

    /// <inheritdoc />
    public override string ToString() => Name;

    /// <summary>Returns all declared values of the given enumeration type.</summary>
    /// <typeparam name="T">The concrete enumeration type.</typeparam>
    public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                 .Select(f => f.GetValue(null))
                 .Cast<T>();

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Enumeration other && GetType() == other.GetType() && Id.Equals(other.Id);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(object? other) => Id.CompareTo(((Enumeration)other!).Id);
}
