using Server.Domain.Currencies;

namespace Server.Application.Common.Interfaces;

/// <summary>Persistence operations for <see cref="CurrencyRate"/> aggregates.</summary>
public interface ICurrencyRateRepository
{
    /// <summary>Returns all currency rates.</summary>
    Task<IReadOnlyList<CurrencyRate>> ListAsync(CancellationToken ct = default);

    /// <summary>Returns the rate with the given id, or null.</summary>
    Task<CurrencyRate?> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Returns the rate for the given currency pair, or null.</summary>
    Task<CurrencyRate?> GetPairAsync(string from, string to, CancellationToken ct = default);

    /// <summary>Tracks a new rate for insertion.</summary>
    Task AddAsync(CurrencyRate rate, CancellationToken ct = default);

    /// <summary>Marks a rate for deletion.</summary>
    void Remove(CurrencyRate rate);
}
