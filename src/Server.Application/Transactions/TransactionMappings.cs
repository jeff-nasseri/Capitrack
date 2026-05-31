using Server.Domain.Transactions;

namespace Server.Application.Transactions;

public static class TransactionMappings
{
    public static TransactionDto ToDto(this Transaction t, string? accountName, List<TagDto> tags) => new(
        t.Id, t.AccountId, t.Symbol.Value, t.Type.Value, t.Quantity.Value, t.Price, t.Fee,
        t.Currency.Value, t.Date.Value, t.Notes, t.CreatedAt, accountName, tags);
}
