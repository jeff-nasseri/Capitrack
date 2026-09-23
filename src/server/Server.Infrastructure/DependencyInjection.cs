using Microsoft.Extensions.DependencyInjection;
using Server.Infrastructure.Persistence;
using Server.Infrastructure.Persistence.Repositories;
using Server.Infrastructure.Services;
using Server.Infrastructure.Services.MarketData;

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
        // Market data: providers receive only symbols and dates; API keys come from the environment only.
        var marketHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        marketHttp.DefaultRequestHeaders.UserAgent.ParseAdd("Capitrack/1.0 (self-hosted)");
        static string? Env(string name) => Environment.GetEnvironmentVariable(name);
        services.AddSingleton<IPriceProvider>(_ => new CoinGeckoProvider(marketHttp, Env("COINGECKO_DEMO_API_KEY")));
        services.AddSingleton<IPriceProvider>(_ => new KrakenProvider(marketHttp));
        services.AddSingleton<IPriceProvider>(_ => new BitvavoProvider(marketHttp));
        services.AddSingleton<IPriceProvider>(sp => new YahooProvider(sp.GetRequiredService<IYahooFinanceClient>()));
        services.AddSingleton<IPriceProvider>(_ => new TwelveDataProvider(marketHttp, Env("TWELVE_DATA_API_KEY")));
        services.AddSingleton<IPriceProvider>(_ => new EcbProvider(marketHttp));
        services.AddScoped<IMarketDataService, MarketDataService>();
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
