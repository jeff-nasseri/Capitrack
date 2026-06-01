namespace Server.Application.Prices;

/// <summary>A single OHLCV point in a price history series.</summary>
/// <param name="Date">The point's timestamp.</param>
/// <param name="Close">The closing price.</param>
/// <param name="Open">The opening price.</param>
/// <param name="High">The session high.</param>
/// <param name="Low">The session low.</param>
/// <param name="Volume">The traded volume.</param>
public record HistoryPointDto(DateTime Date, double? Close, double? Open, double? High, double? Low, double? Volume);
