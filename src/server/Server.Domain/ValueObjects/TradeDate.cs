namespace Server.Domain.ValueObjects;

/// <summary>A calendar date stored/exposed as a yyyy-MM-dd string (the app's persisted format).</summary>
public sealed class TradeDate : ValueObject
{
    /// <summary>The date as a yyyy-MM-dd string.</summary>
    public string Value { get; }
    private TradeDate(string value) => Value = value;

    /// <summary>Creates a trade date from a yyyy-MM-dd or otherwise parseable date string.</summary>
    public static TradeDate Create(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return new TradeDate(raw);
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return new TradeDate(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        throw new InvalidDateException(value ?? "");
    }

    /// <summary>The date as a <see cref="DateOnly"/>.</summary>
    public DateOnly AsDateOnly => DateOnly.ParseExact(Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
