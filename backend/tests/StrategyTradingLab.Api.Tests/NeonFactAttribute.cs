using Microsoft.Extensions.Configuration;
using StrategyTradingLab.Api.Neon;

namespace StrategyTradingLab.Api.Tests;

/// <summary>
/// A test against the linked Neon branch. Skipped when the app wouldn't find its database and
/// bucket (CI); locally they come from the repo's .env.local (`neon env pull`).
/// </summary>
public sealed class NeonFactAttribute : FactAttribute
{
    static readonly bool Configured = IsConfigured();

    public NeonFactAttribute()
    {
        if (!Configured) Skip = "No Neon branch: run `neon env pull` to write .env.local.";
    }

    static bool IsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .AddNeonDotEnv(AppContext.BaseDirectory)
            .Build();
        return NeonEnvironment.ConnectionString(configuration) is not null && ObjectStorage.IsConfigured(configuration);
    }
}
