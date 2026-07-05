using Microsoft.Extensions.DependencyInjection;
using Server.Infrastructure.Persistence;
using Server.Infrastructure.Persistence.Repositories;
using Server.Infrastructure.Services;

namespace Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        var dbPath = DbPathResolver.Resolve();
        var dataDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dataDir)) Directory.CreateDirectory(dataDir);

        services.AddDbContext<CapitrackDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

        // Unit of work + repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<ICurrencyRateRepository, CurrencyRateRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
        services.AddScoped<IBlacklistRepository, BlacklistRepository>();

        // Services
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ISystemService, SystemService>();
        services.AddSingleton<IYahooFinanceClient, YahooFinanceClient>();
        services.AddSingleton<ITotpService, TotpService>();
        services.AddScoped<IPriceService, PriceService>();
        services.AddScoped<IWealthService, WealthService>();
        services.AddScoped<IImporterService, ImporterService>();
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        services.AddScoped<ILoginSecurityService, LoginSecurityService>();

        return services;
    }

    public static void InitializeDatabase(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CapitrackDbContext>();
        db.Database.EnsureCreated();
        // EnsureCreated is a no-op on an existing database, so upgrade its schema to the
        // current model (adds tables/columns introduced since the volume was created).
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger(nameof(SqliteSchemaUpgrader));
        SqliteSchemaUpgrader.Upgrade(db, logger);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        SeedService.Seed(db, hasher);
    }
}
