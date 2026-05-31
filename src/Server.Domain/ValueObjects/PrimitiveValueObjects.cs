namespace Server.Domain.ValueObjects;

/// <summary>An ISO-4217-style currency code (uppercased, non-empty).</summary>
public sealed class CurrencyCode : ValueObject
{
    public string Value { get; }
    private CurrencyCode(string value) => Value = value;

    public static readonly CurrencyCode Eur = new("EUR");
    public static readonly CurrencyCode Usd = new("USD");

    public static CurrencyCode Create(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (v.Length == 0) throw new InvalidCurrencyCodeException(value ?? "");
        return new CurrencyCode(v);
    }

    public static CurrencyCode CreateOrDefault(string? value, CurrencyCode fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : Create(value);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}

/// <summary>A market ticker symbol (uppercased, non-empty), e.g. AAPL, BTC-USD, GC=F.</summary>
public sealed class Symbol : ValueObject
{
    public string Value { get; }
    private Symbol(string value) => Value = value;

    public static Symbol Create(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (v.Length == 0) throw new InvalidSymbolException();
        return new Symbol(v);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}

/// <summary>A non-negative number of units/shares.</summary>
public sealed class Quantity : ValueObject
{
    public double Value { get; }
    private Quantity(double value) => Value = value;

    public static readonly Quantity Zero = new(0);

    public static Quantity Create(double value)
    {
        if (value < 0) throw new NegativeQuantityException(value);
        return new Quantity(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A calendar date stored/exposed as a yyyy-MM-dd string (the app's persisted format).</summary>
public sealed class TradeDate : ValueObject
{
    public string Value { get; }
    private TradeDate(string value) => Value = value;

    public static TradeDate Create(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return new TradeDate(raw);
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return new TradeDate(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        throw new InvalidDateException(value ?? "");
    }

    public DateOnly AsDateOnly => DateOnly.ParseExact(Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => Value;
}

/// <summary>A hex color string such as #6366f1.</summary>
public sealed class Color : ValueObject
{
    private static readonly Regex Hex = new("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$", RegexOptions.Compiled);

    public string Value { get; }
    private Color(string value) => Value = value;

    public static readonly Color Default = new("#6366f1");

    public static Color Create(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (!Hex.IsMatch(v)) throw new InvalidColorException(value ?? "");
        return new Color(v);
    }

    public static Color CreateOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Default : Create(value);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value.ToLowerInvariant(); }
    public override string ToString() => Value;
}

/// <summary>A percentage change value (e.g. +12.34 meaning 12.34%).</summary>
public sealed class Percentage : ValueObject
{
    public double Value { get; }
    public Percentage(double value) => Value = value;

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
    public override string ToString() => $"{Value.ToString("0.##", CultureInfo.InvariantCulture)}%";
}
