using Server.Domain.Accounts;
using Server.Domain.Currencies;
using Server.Domain.Goals;
using Server.Domain.Tags;
using Server.Domain.Transactions;
using Server.Domain.Users;

namespace Server.Application.Common.Interfaces;

public interface IAccountRepository
{
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken ct = default);
    Task<Account?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    void Remove(Account account);
    Task<IReadOnlyList<int>> TagIdsAsync(int accountId, CancellationToken ct = default);
    Task ReplaceTagsAsync(int accountId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);
    Task PurgeAllAsync(CancellationToken ct = default);
}

public interface ITransactionRepository
{
    Task<IReadOnlyList<Transaction>> ListAsync(int? accountId, string? symbol, int? limit, int? offset, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> ForAccountAsync(int accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> AllAsync(CancellationToken ct = default);
    Task<Transaction?> GetAsync(int id, CancellationToken ct = default);
    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    void Remove(Transaction transaction);
    Task<IReadOnlyList<int>> TagIdsAsync(int transactionId, CancellationToken ct = default);
    Task ReplaceTagsAsync(int transactionId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);
}

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> ListAsync(CancellationToken ct = default);
    Task<Tag?> GetAsync(int id, CancellationToken ct = default);
    Task<Tag?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<Tag>> ByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    Task AddAsync(Tag tag, CancellationToken ct = default);
    void Remove(Tag tag);
}

public interface IGoalRepository
{
    Task<IReadOnlyList<Goal>> ListAsync(int? categoryId, int? tagId, CancellationToken ct = default);
    Task<Goal?> GetAsync(int id, CancellationToken ct = default);
    Task AddAsync(Goal goal, CancellationToken ct = default);
    void Remove(Goal goal);
    Task RemoveAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<int>> TagIdsAsync(int goalId, CancellationToken ct = default);
    Task ReplaceTagsAsync(int goalId, IReadOnlyCollection<int> tagIds, CancellationToken ct = default);
}

public interface ICurrencyRateRepository
{
    Task<IReadOnlyList<CurrencyRate>> ListAsync(CancellationToken ct = default);
    Task<CurrencyRate?> GetAsync(int id, CancellationToken ct = default);
    Task<CurrencyRate?> GetPairAsync(string from, string to, CancellationToken ct = default);
    Task AddAsync(CurrencyRate rate, CancellationToken ct = default);
    void Remove(CurrencyRate rate);
}

public interface IUserRepository
{
    Task<User?> GetFirstAsync(CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
