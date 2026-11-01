using Microsoft.Extensions.Configuration;
using StrategyTradingLab.Api.Neon;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// A test against the linked Neon branch. Skipped when the app wouldn't find its database and
/// bucket (CI); locally they come from the repo's .env.local (`neon env pull`).
/// </summary>
public sealed class NeonFactAttribute : FactAttribute
{
    public NeonFactAttribute()
    {
        if (!Configured) Skip = "No Neon branch: run `neon env pull` to write .env.local.";
    }

    /// <summary>Where the tests find the database: the environment, else the repo's .env.local.</summary>
    public static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddUserSecrets<Program>(optional: true)
        .AddEnvironmentVariables()
        .AddNeonDotEnv(AppContext.BaseDirectory)
        .Build();

    // After Configuration, which it reads: static fields start in order.
    public static readonly bool Configured = IsConfigured();

    static bool IsConfigured() =>
        NeonEnvironment.ConnectionString(Configuration) is not null && ObjectStorage.IsConfigured(Configuration);
}
