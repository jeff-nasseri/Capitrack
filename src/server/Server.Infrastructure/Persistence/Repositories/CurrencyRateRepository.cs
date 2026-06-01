using Server.Domain.Currencies;

namespace Server.Infrastructure.Persistence.Repositories;

public sealed class CurrencyRateRepository(CapitrackDbContext db) : ICurrencyRateRepository
{
    public async Task<IReadOnlyList<CurrencyRate>> ListAsync(CancellationToken ct = default) =>
        await db.CurrencyRates.OrderBy(r => r.FromCurrency).ThenBy(r => r.ToCurrency).ToListAsync(ct);

    public Task<CurrencyRate?> GetAsync(int id, CancellationToken ct = default) =>
        db.CurrencyRates.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<CurrencyRate?> GetPairAsync(string from, string to, CancellationToken ct = default)
    {
        var f = CurrencyCode.Create(from);
        var t = CurrencyCode.Create(to);
        return db.CurrencyRates.FirstOrDefaultAsync(r => r.FromCurrency == f && r.ToCurrency == t, ct);
    }

    public Task AddAsync(CurrencyRate rate, CancellationToken ct = default)
    {
        db.CurrencyRates.Add(rate);
        return Task.CompletedTask;
    }

    public void Remove(CurrencyRate rate) => db.CurrencyRates.Remove(rate);
}
