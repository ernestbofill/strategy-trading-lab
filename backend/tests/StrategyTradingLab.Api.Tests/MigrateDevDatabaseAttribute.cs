using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StrategyTradingLab.Api.Data;
using StrategyTradingLab.Api.Neon;
using Xunit.Sdk;

[assembly: StrategyTradingLab.Api.Tests.MigrateDevDatabase]

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// Brings the dev database up to date before the first test of a local run, so a test never fails only because nobody has
/// started the API since the last merge. It applies this checkout's migrations, a branch's too, as <c>dotnet run</c> does.
/// Opt in: only with <c>TESTS_MIGRATE_DEV=true</c> in <c>.env.local</c>, which says that file's database is dev; without it nothing is
/// migrated. Never in CI, whose throwaway database gets <c>migrations.sql</c> first, and it refuses if the pooled or the direct URL
/// is the one <c>DATABASE_URL_PRODUCTION</c> names (when that is set).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class MigrateDevDatabaseAttribute : BeforeAfterTestAttribute
{
    // Once per run; a failure fails every test with the same reason.
    static readonly Lazy<bool> Migrated = new(Migrate);

    public override void Before(MethodInfo methodUnderTest) => _ = Migrated.Value;

    static bool Migrate()
    {
        if (!NeonFactAttribute.Configured || Environment.GetEnvironmentVariable("CI") is not null) return false;
        var configuration = NeonFactAttribute.Configuration;
        var connectionString = NeonEnvironment.ConnectionString(configuration, direct: true)!;
        if (configuration["TESTS_MIGRATE_DEV"] != "true") return false;
        if (Uri.TryCreate(configuration["DATABASE_URL_PRODUCTION"], UriKind.Absolute, out var production)
            && new[] { connectionString, NeonEnvironment.ConnectionString(configuration)! }
                .Any(tested => BranchHost(new NpgsqlConnectionStringBuilder(tested).Host) == BranchHost(production.Host)))
            throw new InvalidOperationException("The tests' database is the production one: fix DATABASE_URL in .env.local. Nothing was migrated.");

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContextOptions.Configure(options, connectionString);
        using var db = new AppDbContext(options.Options);
        var pending = db.Database.GetPendingMigrations().ToList();
        if (pending.Count == 0) return false;
        db.Database.Migrate();
        Console.WriteLine($"Applied to the dev database: {string.Join(", ", pending)}");
        return true;
    }

    /// <summary>A Neon endpoint's host without the pooler suffix, so the pooled and direct URLs of one branch match.</summary>
    static string? BranchHost(string? host) => host?.Replace("-pooler", "", StringComparison.Ordinal).ToLowerInvariant();
}
