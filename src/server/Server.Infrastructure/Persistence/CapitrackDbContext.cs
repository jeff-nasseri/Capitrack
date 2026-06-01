using Server.Domain.Accounts;
using Server.Domain.Categories;
using Server.Domain.Currencies;
using Server.Domain.Goals;
using Server.Domain.Tags;
using Server.Domain.Transactions;
using Server.Domain.Users;

namespace Server.Infrastructure.Persistence;

public class CapitrackDbContext(DbContextOptions<CapitrackDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<PriceCacheRecord> PriceCache => Set<PriceCacheRecord>();
    public DbSet<DailyWealthRecord> DailyWealth => Set<DailyWealthRecord>();
    public DbSet<AccountTag> AccountTags => Set<AccountTag>();
    public DbSet<GoalTag> GoalTags => Set<GoalTag>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.BaseCurrency).HasConversion(v => v.Value, v => CurrencyCode.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<Account>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.Ignore(x => x.Tags);
            e.Property(x => x.Type).HasConversion(v => v.Value, v => AccountType.From(v));
            e.Property(x => x.Currency).HasConversion(v => v.Value, v => CurrencyCode.Create(v));
            e.Property(x => x.Color).HasConversion(v => v.Value, v => Color.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<Transaction>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.Ignore(x => x.Tags);
            e.Ignore(x => x.GrossValue);
            e.Property(x => x.Symbol).HasConversion(v => v.Value, v => Symbol.Create(v));
            e.Property(x => x.Type).HasConversion(v => v.Value, v => TransactionType.From(v));
            e.Property(x => x.Quantity).HasConversion(v => v.Value, v => Quantity.Create(v));
            e.Property(x => x.Currency).HasConversion(v => v.Value, v => CurrencyCode.Create(v));
            e.Property(x => x.Date).HasConversion(v => v.Value, v => TradeDate.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            e.HasIndex(x => x.AccountId);
            e.HasIndex(x => x.Symbol);
            e.HasIndex(x => x.Date);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Color).HasConversion(v => v.Value, v => Color.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            e.HasIndex(x => x.ParentId);
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Tag>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Color).HasConversion(v => v.Value, v => Color.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<Goal>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.Ignore(x => x.Tags);
            e.Property(x => x.TargetDate).HasConversion(v => v.Value, v => TradeDate.Create(v));
            e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
            e.HasIndex(x => x.CategoryId);
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<CurrencyRate>(e =>
        {
            e.Ignore(x => x.DomainEvents);
            e.Property(x => x.FromCurrency).HasConversion(v => v.Value, v => CurrencyCode.Create(v));
            e.Property(x => x.ToCurrency).HasConversion(v => v.Value, v => CurrencyCode.Create(v));
            e.HasIndex(x => new { x.FromCurrency, x.ToCurrency }).IsUnique();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<PriceCacheRecord>(e =>
        {
            e.ToTable("PriceCache");
            e.HasKey(x => x.Symbol);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<DailyWealthRecord>(e =>
        {
            e.ToTable("DailyWealth");
            e.HasKey(x => x.Date);
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        });

        b.Entity<AccountTag>(e =>
        {
            e.HasKey(x => new { x.AccountId, x.TagId });
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GoalTag>(e =>
        {
            e.HasKey(x => new { x.GoalId, x.TagId });
            e.HasOne<Goal>().WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<TransactionTag>(e =>
        {
            e.HasKey(x => new { x.TransactionId, x.TagId });
            e.HasOne<Transaction>().WithMany().HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
