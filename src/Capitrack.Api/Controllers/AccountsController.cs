using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Capitrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public class AccountsController(CapitrackDbContext db) : ControllerBase
{
    private List<TagDto> TagsFor(int accountId) =>
        (from at in db.AccountTags
         join t in db.Tags on at.TagId equals t.Id
         where at.AccountId == accountId
         orderby t.Name
         select new TagDto(t.Id, t.Name, t.Color, t.CreatedAt)).ToList();

    private AccountDto? ToDto(Account? a) => a == null ? null : new AccountDto(
        a.Id, a.Name, a.Type, a.Currency, a.Description, a.Icon, a.Color, a.CreatedAt, a.UpdatedAt, TagsFor(a.Id));

    private void SyncTags(int accountId, List<int>? tagIds)
    {
        db.AccountTags.Where(x => x.AccountId == accountId).ExecuteDelete();
        if (tagIds is { Count: > 0 })
            foreach (var tagId in tagIds.Distinct())
                db.AccountTags.Add(new AccountTag { AccountId = accountId, TagId = tagId });
        db.SaveChanges();
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        var accounts = db.Accounts.OrderByDescending(a => a.CreatedAt).ToList();
        return Ok(accounts.Select(ToDto));
    }

    [HttpDelete("purge/all")]
    public IActionResult PurgeAll()
    {
        db.TransactionTags.ExecuteDelete();
        db.Transactions.ExecuteDelete();
        db.AccountTags.ExecuteDelete();
        db.Accounts.ExecuteDelete();
        db.GoalTags.ExecuteDelete();
        db.Goals.ExecuteDelete();
        db.Tags.ExecuteDelete();
        db.PriceCache.ExecuteDelete();
        return Ok(new { message = "All accounts, transactions, goals, and cached prices have been purged." });
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id);
        if (account == null) return NotFound(new { error = "Account not found" });
        return Ok(ToDto(account));
    }

    [HttpPost]
    public IActionResult Create([FromBody] AccountRequest body)
    {
        if (string.IsNullOrEmpty(body.Name))
            return BadRequest(new { error = "Account name required" });

        var account = new Account
        {
            Name = body.Name,
            Type = string.IsNullOrEmpty(body.Type) ? "general" : body.Type,
            Currency = string.IsNullOrEmpty(body.Currency) ? "EUR" : body.Currency,
            Description = body.Description ?? "",
            Icon = string.IsNullOrEmpty(body.Icon) ? "wallet" : body.Icon,
            Color = string.IsNullOrEmpty(body.Color) ? "#6366f1" : body.Color
        };
        db.Accounts.Add(account);
        db.SaveChanges();
        if (body.TagIds is { Count: > 0 }) SyncTags(account.Id, body.TagIds);
        return StatusCode(201, ToDto(db.Accounts.First(a => a.Id == account.Id)));
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] AccountRequest body)
    {
        var existing = db.Accounts.FirstOrDefault(a => a.Id == id);
        if (existing == null) return NotFound(new { error = "Account not found" });

        existing.Name = string.IsNullOrEmpty(body.Name) ? existing.Name : body.Name;
        existing.Type = string.IsNullOrEmpty(body.Type) ? existing.Type : body.Type;
        existing.Currency = string.IsNullOrEmpty(body.Currency) ? existing.Currency : body.Currency;
        existing.Description = body.Description ?? existing.Description;
        existing.Icon = string.IsNullOrEmpty(body.Icon) ? existing.Icon : body.Icon;
        existing.Color = string.IsNullOrEmpty(body.Color) ? existing.Color : body.Color;
        existing.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();

        if (body.TagIds != null) SyncTags(id, body.TagIds);
        return Ok(ToDto(db.Accounts.First(a => a.Id == id)));
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = db.Accounts.FirstOrDefault(a => a.Id == id);
        if (existing == null) return NotFound(new { error = "Account not found" });
        db.Accounts.Remove(existing);
        db.SaveChanges();
        return Ok(new { message = "Account deleted" });
    }

    [HttpGet("{id:int}/holdings")]
    public IActionResult Holdings(int id)
    {
        var account = db.Accounts.FirstOrDefault(a => a.Id == id);
        if (account == null) return NotFound(new { error = "Account not found" });
        var txs = db.Transactions.Where(t => t.AccountId == id).ToList();
        return Ok(HoldingsCalculator.AccountHoldings(txs));
    }
}
