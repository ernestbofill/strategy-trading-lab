using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Data;

/// <summary>
/// Used by <c>dotnet ef</c>. Migrations run over the direct connection (DATABASE_URL_UNPOOLED):
/// PgBouncer's transaction mode doesn't keep the session state they rely on.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .AddNeonDotEnv(Directory.GetCurrentDirectory())
            .Build();

        // `migrations add` and `migrations script` don't connect, so a placeholder is enough there.
        var connectionString = NeonEnvironment.ConnectionString(configuration, direct: true)
            ?? "Host=localhost;Database=strategy_trading_lab";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, connectionString);
        return new AppDbContext(options.Options);
    }
}

public static class AppDbContextOptions
{
    // Stateless, so one instance serves every context.
    static readonly UpdatedAtInterceptor UpdatedAt = new(TimeProvider.System);

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(UpdatedAt);
}
