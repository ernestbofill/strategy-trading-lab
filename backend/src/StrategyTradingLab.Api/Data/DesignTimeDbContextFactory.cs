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
        NeonEnvironment.LoadDotEnv(Directory.GetCurrentDirectory());
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
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
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new UpdatedAtInterceptor(TimeProvider.System));
}
