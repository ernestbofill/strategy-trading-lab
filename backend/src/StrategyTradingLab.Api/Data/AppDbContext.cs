using Microsoft.EntityFrameworkCore;
using StrategyTradingLab.Api.Data.Entities;
using StrategyTradingLab.Api.Templates;

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
    public DbSet<SignalTrade> SignalTrades => Set<SignalTrade>();
    public DbSet<UserTrade> UserTrades => Set<UserTrade>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<EmailToken> EmailTokens => Set<EmailToken>();
    public DbSet<Invite> Invites => Set<Invite>();
    public DbSet<PriceDaily> Prices => Set<PriceDaily>();
    public DbSet<FxRate> FxRates => Set<FxRate>();
    public DbSet<DailyEvaluation> DailyEvaluations => Set<DailyEvaluation>();
    public DbSet<FeedbackItem> FeedbackItems => Set<FeedbackItem>();
    public DbSet<AiRound> AiRounds => Set<AiRound>();
    public DbSet<SharedAiRound> SharedAiRounds => Set<SharedAiRound>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        foreach (var enumType in SnakeCaseEnums.Types)
        {
            configurationBuilder.Properties(enumType).HaveConversion(SnakeCaseEnums.ConverterFor(enumType));
        }

        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyTableConventions();
        TemplateAssets.Apply(modelBuilder);
    }
}
