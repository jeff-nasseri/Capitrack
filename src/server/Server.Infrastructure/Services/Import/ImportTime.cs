using System.Globalization;
using System.Text.RegularExpressions;

namespace Server.Infrastructure.Services.Import;

/// <summary>When an imported row happened: the exact UTC instant (if known) and the calendar dates derived from it.</summary>
/// <param name="Utc">The exact instant in UTC, or null when the source only has a date.</param>
/// <param name="Date">The UTC calendar date (yyyy-MM-dd) — the date daily prices are looked up for.</param>
/// <param name="LocalDate">The date as written in the source's local time, when it differs in meaning (used to recognise rows imported by older versions).</param>
public sealed record ImportTime(DateTime? Utc, string Date, string? LocalDate);

/// <summary>Timestamp parsing shared by the CSV importers.</summary>
public static partial class ImportTimeParser
{
    /// <summary>
    /// Trezor Suite: the Unix "Timestamp" column is authoritative (UTC). "Date"/"Time" are local
    /// ("9/7/2026", "10:04:51 PM GMT+2" — the offset changes with DST) and only a fallback.
    /// </summary>
    public static ImportTime? Trezor(string? timestamp, string? date, string? time)
    {
        var localDate = UsDate(date);
        if (long.TryParse((timestamp ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var unix) && unix > 0)
        {
            var utc = (unix > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix)).UtcDateTime;
            return new ImportTime(utc, DateString(utc), localDate);
        }

        if (localDate is null) return null;
        var offset = GmtOffset(time);
        if (offset is not null && DateTime.TryParse($"{localDate} {TimeWithoutZone(time)}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            var utc = new DateTimeOffset(local, offset.Value).UtcDateTime;
            return new ImportTime(utc, DateString(utc), localDate);
        }
        return new ImportTime(null, localDate, localDate);
    }

    /// <summary>
    /// An ISO-8601 style date or date-time. With an explicit offset/Z it is converted to UTC; a bare
    /// date-time is taken as UTC (the source gives no zone); a bare date has no instant.
    /// </summary>
    public static ImportTime? Iso(string? value)
    {
        var s = (value ?? "").Trim();
        if (s.Length == 0) return null;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return new ImportTime(null, DateString(d), null);
        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return new ImportTime(dto.UtcDateTime, DateString(dto.UtcDateTime), null);
        return null;
    }

    private static string DateString(DateTime utc) => utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string DateString(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"9/7/2026" (month/day/year, as Trezor Suite writes it) → "2026-09-07".</summary>
    private static string? UsDate(string? value)
    {
        var parts = (value ?? "").Trim().Split('/');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var m) || !int.TryParse(parts[1], out var d) || !int.TryParse(parts[2], out var y))
            return null;
        try { return DateString(new DateOnly(y, m, d)); } catch (ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>"GMT+2" / "GMT-05:30" / "UTC" → offset.</summary>
    private static TimeSpan? GmtOffset(string? time)
    {
        var m = OffsetRegex().Match(time ?? "");
        if (!m.Success) return null;
        if (m.Groups["utc"].Success && !m.Groups["sign"].Success) return TimeSpan.Zero;
        var hours = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minutes = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        var span = new TimeSpan(hours, minutes, 0);
        return m.Groups["sign"].Value == "-" ? -span : span;
    }

    private static string TimeWithoutZone(string? time) => OffsetRegex().Replace(time ?? "", "").Trim();

    [GeneratedRegex(@"(?<utc>GMT|UTC)(?:(?<sign>[+-])(?<h>\d{1,2})(?::?(?<m>\d{2}))?)?", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetRegex();
}
