using System.Globalization;
using System.Text;
using Capitrack.Api.Data;
using Capitrack.Api.Models;
using Capitrack.Api.Services;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Capitrack.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController(CapitrackDbContext db, ImporterService importer) : ControllerBase
{
    private List<TagDto> TagsFor(int txId) =>
        (from tt in db.TransactionTags
         join t in db.Tags on tt.TagId equals t.Id
         where tt.TransactionId == txId
         orderby t.Name
         select new TagDto(t.Id, t.Name, t.Color, t.CreatedAt)).ToList();

    private TransactionDto ToDto(Transaction t, string? accountName) => new(
        t.Id, t.AccountId, t.Symbol, t.Type, t.Quantity, t.Price, t.Fee, t.Currency,
        t.Date, t.Notes, t.CreatedAt, accountName, TagsFor(t.Id));

    private void SyncTags(int txId, List<int>? tagIds)
    {
        db.TransactionTags.Where(x => x.TransactionId == txId).ExecuteDelete();
        if (tagIds is { Count: > 0 })
            foreach (var tagId in tagIds.Distinct())
                db.TransactionTags.Add(new TransactionTag { TransactionId = txId, TagId = tagId });
        db.SaveChanges();
    }

    [HttpGet]
    public IActionResult GetAll([FromQuery(Name = "account_id")] int? accountId,
        [FromQuery] string? symbol, [FromQuery] int? limit, [FromQuery] int? offset)
    {
        IQueryable<Transaction> q = db.Transactions;
        if (accountId is int aid) q = q.Where(t => t.AccountId == aid);
        if (!string.IsNullOrEmpty(symbol)) q = q.Where(t => t.Symbol == symbol);
        q = q.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        if (offset is int off) q = q.Skip(off);
        if (limit is int lim) q = q.Take(lim);

        var txs = q.ToList();
        var names = db.Accounts.ToDictionary(a => a.Id, a => a.Name);
        return Ok(txs.Select(t => ToDto(t, names.GetValueOrDefault(t.AccountId))));
    }

    [HttpGet("{id:int}")]
    public IActionResult Get(int id)
    {
        var t = db.Transactions.FirstOrDefault(x => x.Id == id);
        if (t == null) return NotFound(new { error = "Transaction not found" });
        var name = db.Accounts.Where(a => a.Id == t.AccountId).Select(a => a.Name).FirstOrDefault();
        return Ok(ToDto(t, name));
    }

    [HttpPost]
    public IActionResult Create([FromBody] TransactionRequest body)
    {
        if (body.AccountId == null || string.IsNullOrEmpty(body.Symbol) || string.IsNullOrEmpty(body.Type) || string.IsNullOrEmpty(body.Date))
            return BadRequest(new { error = "account_id, symbol, type, and date are required" });
        if (!db.Accounts.Any(a => a.Id == body.AccountId))
            return NotFound(new { error = "Account not found" });

        var tx = new Transaction
        {
            AccountId = body.AccountId.Value,
            Symbol = body.Symbol.ToUpperInvariant(),
            Type = body.Type,
            Quantity = body.Quantity ?? 0,
            Price = body.Price ?? 0,
            Fee = body.Fee ?? 0,
            Currency = string.IsNullOrEmpty(body.Currency) ? "EUR" : body.Currency,
            Date = body.Date,
            Notes = body.Notes ?? ""
        };
        db.Transactions.Add(tx);
        db.SaveChanges();
        if (body.TagIds is { Count: > 0 }) SyncTags(tx.Id, body.TagIds);
        return StatusCode(201, ToDto(db.Transactions.First(x => x.Id == tx.Id), null));
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] TransactionRequest body)
    {
        var existing = db.Transactions.FirstOrDefault(x => x.Id == id);
        if (existing == null) return NotFound(new { error = "Transaction not found" });

        existing.AccountId = body.AccountId ?? existing.AccountId;
        existing.Symbol = (string.IsNullOrEmpty(body.Symbol) ? existing.Symbol : body.Symbol).ToUpperInvariant();
        existing.Type = string.IsNullOrEmpty(body.Type) ? existing.Type : body.Type;
        existing.Quantity = body.Quantity ?? existing.Quantity;
        existing.Price = body.Price ?? existing.Price;
        existing.Fee = body.Fee ?? existing.Fee;
        existing.Currency = string.IsNullOrEmpty(body.Currency) ? existing.Currency : body.Currency;
        existing.Date = string.IsNullOrEmpty(body.Date) ? existing.Date : body.Date;
        existing.Notes = body.Notes ?? existing.Notes;
        db.SaveChanges();

        if (body.TagIds != null) SyncTags(id, body.TagIds);
        return Ok(ToDto(db.Transactions.First(x => x.Id == id), null));
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = db.Transactions.FirstOrDefault(x => x.Id == id);
        if (existing == null) return NotFound(new { error = "Transaction not found" });
        db.Transactions.Remove(existing);
        db.SaveChanges();
        return Ok(new { message = "Transaction deleted" });
    }

    [HttpGet("export/csv")]
    public IActionResult ExportCsv([FromQuery(Name = "account_id")] int? accountId)
    {
        IQueryable<Transaction> q = db.Transactions;
        if (accountId is int aid) q = q.Where(t => t.AccountId == aid);
        var txs = q.OrderByDescending(t => t.Date).ToList();
        var names = db.Accounts.ToDictionary(a => a.Id, a => a.Name);

        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        using (var csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
        {
            foreach (var h in new[] { "id", "account_name", "symbol", "type", "quantity", "price", "fee", "currency", "date", "notes" })
                csv.WriteField(h);
            csv.NextRecord();
            foreach (var t in txs)
            {
                csv.WriteField(t.Id);
                csv.WriteField(names.GetValueOrDefault(t.AccountId, ""));
                csv.WriteField(t.Symbol);
                csv.WriteField(t.Type);
                csv.WriteField(t.Quantity);
                csv.WriteField(t.Price);
                csv.WriteField(t.Fee);
                csv.WriteField(t.Currency);
                csv.WriteField(t.Date);
                csv.WriteField(t.Notes);
                csv.NextRecord();
            }
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "transactions.csv");
    }

    [HttpPost("import/csv")]
    public IActionResult ImportCsv()
    {
        var file = Request.Form.Files.GetFile("file");
        if (file == null) return BadRequest(new { error = "CSV file required" });
        var accountIdStr = Request.Form["account_id"].ToString();
        if (string.IsNullOrEmpty(accountIdStr)) return BadRequest(new { error = "account_id required" });
        if (!int.TryParse(accountIdStr, out var accountId) || !db.Accounts.Any(a => a.Id == accountId))
            return NotFound(new { error = "Account not found" });

        var format = Request.Form["format"].ToString();
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = reader.ReadToEnd();
            var result = importer.ImportCsv(content, accountId, string.IsNullOrEmpty(format) ? null : format);
            return Ok(result);
        }
        catch (Exception e)
        {
            return BadRequest(new { error = "Failed to import CSV: " + e.Message });
        }
    }

    [HttpPost("import/detect")]
    public IActionResult DetectCsv()
    {
        var file = Request.Form.Files.GetFile("file");
        if (file == null) return BadRequest(new { error = "CSV file required" });
        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var content = reader.ReadToEnd();
            var (format, headers) = importer.Detect(content);
            return Ok(new { format, headers });
        }
        catch (Exception e)
        {
            return BadRequest(new { error = "Failed to parse CSV: " + e.Message });
        }
    }
}
