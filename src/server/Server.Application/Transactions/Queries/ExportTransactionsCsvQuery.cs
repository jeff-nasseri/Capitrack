using System.Globalization;
using System.Text;

namespace Server.Application.Transactions.Queries;

public record ExportTransactionsCsvQuery(int? AccountId) : IRequest<string>;

public sealed class ExportTransactionsCsvQueryHandler(
    ITransactionRepository transactions,
    IAccountRepository accounts)
    : IRequestHandler<ExportTransactionsCsvQuery, string>
{
    private static readonly string[] Header =
        { "id", "account_name", "symbol", "type", "quantity", "price", "fee", "currency", "date", "notes" };

    public async Task<string> Handle(ExportTransactionsCsvQuery request, CancellationToken cancellationToken)
    {
        var items = await transactions.ListAsync(request.AccountId, null, null, null, cancellationToken);
        var names = (await accounts.ListAsync(cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        var sb = new StringBuilder();
        WriteRecord(sb, Header);
        foreach (var t in items)
        {
            WriteRecord(sb, new[]
            {
                t.Id.ToString(CultureInfo.InvariantCulture),
                names.GetValueOrDefault(t.AccountId, ""),
                t.Symbol.Value,
                t.Type.Value,
                t.Quantity.Value.ToString(CultureInfo.InvariantCulture),
                t.Price.ToString(CultureInfo.InvariantCulture),
                t.Fee.ToString(CultureInfo.InvariantCulture),
                t.Currency.Value,
                t.Date.Value,
                t.Notes
            });
        }
        return sb.ToString();
    }

    // Mirrors CsvHelper's default writer (InvariantCulture): comma delimiter, CRLF record
    // terminator, and minimal quoting (quote only when the field contains a delimiter, quote,
    // CR, LF, or has leading/trailing whitespace; embedded quotes are doubled).
    private static void WriteRecord(StringBuilder sb, string[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Escape(fields[i]));
        }
        sb.Append("\r\n");
    }

    private static string Escape(string field)
    {
        var needsQuote = field.Length > 0
            && (field.Contains(',') || field.Contains('"') || field.Contains('\r') || field.Contains('\n')
                || char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1]));
        if (!needsQuote) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
