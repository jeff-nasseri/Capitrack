using System.Globalization;
using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/currencies")]
public class CurrenciesController(CapitrackDbContext db) : ControllerBase
{
    private static CurrencyRateDto ToDto(CurrencyRate r) => new(r.Id, r.FromCurrency, r.ToCurrency, r.Rate, r.UpdatedAt);

    [HttpGet]
    public IActionResult GetAll() =>
        Ok(db.CurrencyRates.OrderBy(r => r.FromCurrency).ThenBy(r => r.ToCurrency).ToList().Select(ToDto));

    [HttpPost]
    public IActionResult Upsert([FromBody] RateRequest body)
    {
        if (string.IsNullOrEmpty(body.FromCurrency) || string.IsNullOrEmpty(body.ToCurrency) || body.Rate == null)
            return BadRequest(new { error = "from_currency, to_currency, and rate required" });

        var from = body.FromCurrency.ToUpperInvariant();
        var to = body.ToCurrency.ToUpperInvariant();
        var existing = db.CurrencyRates.FirstOrDefault(r => r.FromCurrency == from && r.ToCurrency == to);
        if (existing == null)
            db.CurrencyRates.Add(new CurrencyRate { FromCurrency = from, ToCurrency = to, Rate = body.Rate.Value, UpdatedAt = DateTime.UtcNow });
        else { existing.Rate = body.Rate.Value; existing.UpdatedAt = DateTime.UtcNow; }
        db.SaveChanges();

        var saved = db.CurrencyRates.First(r => r.FromCurrency == from && r.ToCurrency == to);
        return StatusCode(201, ToDto(saved));
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] RateRequest body)
    {
        var existing = db.CurrencyRates.FirstOrDefault(r => r.Id == id);
        if (existing == null) return NotFound(new { error = "Rate not found" });

        existing.FromCurrency = (string.IsNullOrEmpty(body.FromCurrency) ? existing.FromCurrency : body.FromCurrency).ToUpperInvariant();
        existing.ToCurrency = (string.IsNullOrEmpty(body.ToCurrency) ? existing.ToCurrency : body.ToCurrency).ToUpperInvariant();
        existing.Rate = body.Rate ?? existing.Rate;
        existing.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();
        return Ok(ToDto(existing));
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = db.CurrencyRates.FirstOrDefault(r => r.Id == id);
        if (existing == null) return NotFound(new { error = "Rate not found" });
        db.CurrencyRates.Remove(existing);
        db.SaveChanges();
        return Ok(new { message = "Rate deleted" });
    }

    [HttpGet("convert")]
    public IActionResult Convert([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? amount)
    {
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.IsNullOrEmpty(amount))
            return BadRequest(new { error = "from, to, and amount required" });

        var amt = double.TryParse(amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : 0;
        if (from.ToUpperInvariant() == to.ToUpperInvariant())
            return Ok(new { result = amt, rate = 1.0 });

        var rate = db.CurrencyRates
            .Where(r => r.FromCurrency == from.ToUpperInvariant() && r.ToCurrency == to.ToUpperInvariant())
            .Select(r => (double?)r.Rate).FirstOrDefault();
        if (rate == null) return NotFound(new { error = $"No rate found for {from} to {to}" });

        return Ok(new { result = amt * rate.Value, rate = rate.Value });
    }
}
