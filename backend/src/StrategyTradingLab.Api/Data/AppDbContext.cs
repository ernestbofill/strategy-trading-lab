using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;

namespace StrategyTradingLab.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetText> AssetTexts => Set<AssetText>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<StrategyAsset> StrategyAssets => Set<StrategyAsset>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<RuleTrade> RuleTrades => Set<RuleTrade>();
    public DbSet<UserTrade> UserTrades => Set<UserTrade>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every enum in Data.Entities is stored as snake_case text.
        var enumTypes = typeof(AssetKind).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(AssetKind).Namespace);
        foreach (var enumType in enumTypes)
        {
            configurationBuilder.Properties(enumType)
                .HaveConversion(typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType));
        }

        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        DevSeed.Apply(modelBuilder);
    }
}
